#!/usr/bin/env python3
"""Verify the shared extractor against the frozen P0 normalized snapshot."""

from __future__ import annotations

import argparse
import json
import pathlib
import shutil
import subprocess
import sys
import uuid

from focus_registry import RegistryError, require


OUTPUT_FILES = (
    "agent-turns.csv",
    "agents.csv",
    "analysis.json",
    "commits.csv",
    "model-usage.csv",
    "task-files.csv",
    "task-groups.csv",
    "user-messages.csv",
)


def build_parser() -> argparse.ArgumentParser:
    script_dir = pathlib.Path(__file__).resolve().parent
    repo = script_dir.parents[3]
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", type=pathlib.Path, default=repo)
    parser.add_argument(
        "--sessions-dir", type=pathlib.Path,
        default=pathlib.Path.home() / ".codex" / "sessions",
    )
    parser.add_argument(
        "--manifest", type=pathlib.Path,
        default=script_dir.parent / "references" / "p0-closeout-parity.report.json",
    )
    parser.add_argument(
        "--frozen-data", type=pathlib.Path,
        default=repo / "docs" / "codex" / "p0-closeout-session-investigation" / "data",
    )
    return parser


def normalized_analysis(path: pathlib.Path, generated: bool) -> dict:
    value = json.loads(path.read_text(encoding="utf-8"))
    if generated:
        removed = value["source"].pop("event_cutoff_at", None)
        require(removed is not None, "generated analysis is missing explicit event_cutoff_at")
    return value


def main() -> None:
    args = build_parser().parse_args()
    repo = args.repo.resolve()
    scratch = repo / ".tmp"
    scratch.mkdir(parents=True, exist_ok=True)
    output = scratch / f"session-analysis-parity-{uuid.uuid4().hex}"
    output.mkdir()
    try:
        command = [
            sys.executable, "-B", str(pathlib.Path(__file__).with_name("run_analysis.py")),
            "--manifest", str(args.manifest.resolve()),
            "--sessions-dir", str(args.sessions_dir.resolve()),
            "--repo", str(repo),
            "--output-dir", str(output),
            "--quiet", "--parity-output",
        ]
        result = subprocess.run(command, cwd=repo, capture_output=True, text=True, check=False)
        if result.returncode:
            raise RegistryError(
                f"parity extraction failed ({result.returncode}): "
                f"{result.stderr.strip() or result.stdout.strip()}"
            )

        actual_names = tuple(sorted(path.name for path in output.iterdir() if path.is_file()))
        require(actual_names == tuple(sorted(OUTPUT_FILES)), f"unexpected parity outputs: {actual_names}")
        for name in OUTPUT_FILES:
            generated_path = output / name
            frozen_path = args.frozen_data.resolve() / name
            require(frozen_path.is_file(), f"missing frozen parity file: {frozen_path}")
            if name == "analysis.json":
                require(
                    normalized_analysis(generated_path, generated=True) ==
                    normalized_analysis(frozen_path, generated=False),
                    "analysis.json differs beyond the expected explicit event_cutoff_at field",
                )
            else:
                require(
                    generated_path.read_bytes() == frozen_path.read_bytes(),
                    f"{name} differs from the frozen P0 snapshot",
                )
    finally:
        shutil.rmtree(output)
    print("Extractor parity verified against the frozen P0 snapshot")


if __name__ == "__main__":
    try:
        main()
    except RegistryError as error:
        raise SystemExit(str(error)) from error

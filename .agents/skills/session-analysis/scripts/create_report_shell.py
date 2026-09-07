#!/usr/bin/env python3
"""Create a minimal self-contained HTML shell for a new session report."""

from __future__ import annotations

import argparse
import html
import pathlib

from focus_registry import RegistryError, require
from report_manifest import read_manifest, verify_self_contained_html


def render_report_shell(manifest: dict) -> str:
    title = html.escape(manifest["title"])
    summary = html.escape(manifest["summary"])
    eyebrow = html.escape(manifest["eyebrow"])
    published_at = html.escape(manifest["published_at"])
    return f"""<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>{title}</title>
  <style>
    :root {{ color-scheme: light; --bg: #f4efe6; --panel: #fffaf2; --text: #211c18; --muted: #6d6258; --accent: #a94f30; }}
    * {{ box-sizing: border-box; }}
    body {{ margin: 0; background: var(--bg); color: var(--text); font-family: "Segoe UI", sans-serif; }}
    main {{ width: min(1100px, calc(100% - 32px)); margin: 32px auto 64px; }}
    header, section {{ background: var(--panel); border-radius: 22px; padding: 24px; box-shadow: 0 18px 55px rgba(60, 42, 28, .1); }}
    section {{ margin-top: 18px; }}
    .eyebrow {{ color: var(--accent); font-size: .78rem; font-weight: 700; letter-spacing: .12em; text-transform: uppercase; }}
    h1 {{ margin: 8px 0 12px; font-size: clamp(2rem, 5vw, 3.6rem); line-height: 1.05; }}
    p {{ color: var(--muted); line-height: 1.6; }}
  </style>
</head>
<body>
  <main>
    <header>
      <span class="eyebrow">{eyebrow} · {published_at}</span>
      <h1>{title}</h1>
      <p>{summary}</p>
    </header>
    <section id="analysis-root">
      <h2>Question-driven analysis</h2>
      <p>Replace this placeholder with the report-specific evidence, findings, limitations, and visual form.</p>
    </section>
  </main>
</body>
</html>
"""


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", type=pathlib.Path, required=True)
    parser.add_argument("--repo", type=pathlib.Path, default=pathlib.Path("."))
    return parser


def main() -> None:
    args = build_parser().parse_args()
    repo = args.repo.resolve()
    manifest = read_manifest(args.manifest.resolve())
    destination = (repo / manifest["site_path"] / manifest["html_file"]).resolve()
    try:
        destination.relative_to(repo)
    except ValueError as error:
        raise RegistryError("report HTML destination must be inside the repository") from error
    require(not destination.exists(), f"refusing to overwrite existing report HTML: {destination}")
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(render_report_shell(manifest), encoding="utf-8", newline="\n")
    verify_self_contained_html(destination)
    print(f"Created self-contained report shell at {destination.relative_to(repo).as_posix()}")


if __name__ == "__main__":
    try:
        main()
    except RegistryError as error:
        raise SystemExit(str(error)) from error

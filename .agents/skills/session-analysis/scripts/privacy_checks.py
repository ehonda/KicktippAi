#!/usr/bin/env python3
"""Deterministic privacy checks shared by extraction and publication validation."""

from __future__ import annotations

import pathlib
import re
from collections.abc import Iterable

from focus_registry import require


TEXT_SUFFIXES = {".csv", ".css", ".html", ".js", ".json", ".md", ".txt"}
SECRET_PATTERNS = (
    re.compile(r"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----"),
    re.compile(r"\b(?:sk-(?:proj-)?|ghp_|github_pat_|glpat-)[A-Za-z0-9_-]{16,}"),
    re.compile(r"\bAKIA[0-9A-Z]{16}\b"),
    re.compile(r"\bBearer\s+[A-Za-z0-9._~+/=-]{20,}", flags=re.IGNORECASE),
)


def iter_text_files(paths: Iterable[pathlib.Path]) -> Iterable[pathlib.Path]:
    seen: set[pathlib.Path] = set()
    for raw_path in paths:
        path = raw_path.resolve()
        candidates = path.rglob("*") if path.is_dir() else (path,)
        for candidate in candidates:
            if not candidate.is_file() or candidate.suffix.lower() not in TEXT_SUFFIXES:
                continue
            resolved = candidate.resolve()
            if resolved not in seen:
                seen.add(resolved)
                yield resolved


def verify_text_privacy(paths: Iterable[pathlib.Path]) -> None:
    home = str(pathlib.Path.home())
    home_variants = {home.casefold(), home.replace("\\", "/").casefold()}
    for path in iter_text_files(paths):
        content = path.read_text(encoding="utf-8", errors="replace")
        folded_content = content.casefold()
        require(
            not any(value and value in folded_content for value in home_variants),
            f"private user-home path remains in publishable output: {path}",
        )
        require(
            not any(pattern.search(content) for pattern in SECRET_PATTERNS),
            f"possible credential remains in publishable output: {path}",
        )

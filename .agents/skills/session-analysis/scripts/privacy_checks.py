#!/usr/bin/env python3
"""Deterministic privacy checks shared by extraction and publication validation."""

from __future__ import annotations

import pathlib
import re
from collections.abc import Iterable

from focus_registry import require


SECRET_PATTERNS = (
    re.compile(r"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----"),
    re.compile(r"\b(?:sk-(?:proj-)?|ghp_|github_pat_|glpat-)[A-Za-z0-9_-]{16,}"),
    re.compile(r"\bAKIA[0-9A-Z]{16}\b"),
    re.compile(r"\bBearer\s+[A-Za-z0-9._~+/=-]{20,}", flags=re.IGNORECASE),
)
GENERIC_HOME_PATTERNS = (
    re.compile(
        r"(?i)(?<![A-Za-z0-9])(?:"
        r"[A-Z]:[\\/]+(?:Users|Documents and Settings)[\\/]+[^\\/\s\"'<>]+|"
        r"/(?:home|Users)/[^/\s\"'<>]+|"
        r"/root/(?:\.[^/\s\"'<>]+|private(?=[/\s\"'<>]|$)|"
        r"[^/\s\"'<>]+\.[A-Za-z0-9]{1,8}(?:/|$))|"
        r"/var/root(?=[/\s\"'<>]|$))"
    ),
)


def iter_text_files(paths: Iterable[pathlib.Path]) -> Iterable[pathlib.Path]:
    seen: set[pathlib.Path] = set()
    for raw_path in paths:
        path = raw_path.resolve()
        candidates = path.rglob("*") if path.is_dir() else (path,)
        for candidate in candidates:
            if not candidate.is_file():
                continue
            resolved = candidate.resolve()
            if resolved not in seen:
                seen.add(resolved)
                yield resolved


def read_text_if_publishable(path: pathlib.Path) -> str | None:
    data = path.read_bytes()
    if data.startswith((b"\xff\xfe", b"\xfe\xff")):
        return data.decode("utf-16")
    try:
        return data.decode("utf-8-sig")
    except UnicodeDecodeError:
        if b"\x00" in data:
            return None
        sample = data[:8192]
        controls = sum(byte < 32 and byte not in (9, 10, 13) for byte in sample)
        if sample and controls / len(sample) > 0.05:
            return None
        return data.decode("cp1252")


def verify_text_privacy(paths: Iterable[pathlib.Path]) -> None:
    home = str(pathlib.Path.home())
    home_variants = {home.casefold(), home.replace("\\", "/").casefold()}
    for path in iter_text_files(paths):
        content = read_text_if_publishable(path)
        if content is None:
            continue
        folded_content = content.casefold()
        require(
            not any(value and value in folded_content for value in home_variants) and
            not any(pattern.search(content) for pattern in GENERIC_HOME_PATTERNS),
            f"private user-home path remains in publishable output: {path}",
        )
        require(
            not any(pattern.search(content) for pattern in SECRET_PATTERNS),
            f"possible credential remains in publishable output: {path}",
        )

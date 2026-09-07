#!/usr/bin/env python3
"""Explicit companion entrypoint for the shared focus-registry helper."""

from __future__ import annotations

import pathlib
import sys


SHARED_SCRIPTS = pathlib.Path(__file__).resolve().parents[2] / "session-analysis" / "scripts"
sys.path.insert(0, str(SHARED_SCRIPTS))

from focus_registry import RegistryError, main  # noqa: E402


if __name__ == "__main__":
    try:
        main()
    except RegistryError as error:
        raise SystemExit(str(error)) from error

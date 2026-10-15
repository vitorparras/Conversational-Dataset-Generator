"""The entry point refuses bad arguments and a missing secret with the usage exit code."""

import io
import sys

import pytest

from language_service.__main__ import main
from language_service.startup import EXIT_USAGE, MISSING_SECRET_MESSAGE, USAGE


def test_main_without_port_exits_with_usage(capsys: pytest.CaptureFixture[str]) -> None:
    code = main([])

    assert (code, capsys.readouterr().err) == (EXIT_USAGE, USAGE + "\n")


def test_main_without_secret_exits_with_usage(
    monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    monkeypatch.setattr(sys, "stdin", io.TextIOWrapper(io.BytesIO(b"")))

    code = main(["--port", "8123"])

    assert (code, capsys.readouterr().err) == (EXIT_USAGE, MISSING_SECRET_MESSAGE + "\n")

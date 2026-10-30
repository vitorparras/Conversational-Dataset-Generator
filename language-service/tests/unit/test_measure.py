"""The measuring tool refuses, in Portuguese, to run anywhere but Linux."""

import importlib.util
import sys
from pathlib import Path
from types import ModuleType

import pytest

TOOL = Path(__file__).resolve().parents[2] / "tools" / "measure.py"
MODEL = "pt_core_news_md"


def _load_tool(monkeypatch: pytest.MonkeyPatch) -> ModuleType:
    """Imports the script by path (``tools/`` is no package); its dataclasses need the registry."""
    spec = importlib.util.spec_from_file_location("measure_tool", TOOL)
    assert spec is not None
    assert spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    monkeypatch.setitem(sys.modules, spec.name, module)
    spec.loader.exec_module(module)
    return module


@pytest.mark.parametrize("platform", ["win32", "darwin"])
def test_measuring_outside_linux_says_it_only_measures_on_linux(
    platform: str, monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    tool = _load_tool(monkeypatch)
    monkeypatch.setattr(sys, "platform", platform)

    exit_code = tool.main([MODEL])

    captured = capsys.readouterr()
    assert exit_code == 1
    assert captured.out == ""
    assert captured.err.startswith("measure.py: esta ferramenta mede só no Linux")


@pytest.mark.parametrize("platform", ["win32", "darwin"])
def test_the_child_probe_is_refused_outside_linux_too(
    platform: str, monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    tool = _load_tool(monkeypatch)
    monkeypatch.setattr(sys, "platform", platform)

    exit_code = tool.main(["--probe", MODEL])

    assert exit_code == 1
    assert "só no Linux" in capsys.readouterr().err

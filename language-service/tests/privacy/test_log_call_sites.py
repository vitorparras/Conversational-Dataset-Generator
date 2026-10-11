"""Issue 33 CA-5: new code that writes text or an offset to the log is rejected, at the line that did it.

The rule is read from the source with ``ast``: every call of ``.event(...)`` uses only the closed
fields as keywords and no ``**`` expansion, and nothing in the package writes anywhere except
through the service log, apart from the places named in ``ALLOWED_WRITERS``.
"""

import ast
from pathlib import Path

import pytest

import language_service
from language_service.logging_ import ALLOWED_FIELDS

pytestmark = pytest.mark.privacy

PACKAGE = Path(language_service.__file__).parent

# (file, function) that may write outside the service log, and why.
ALLOWED_WRITERS = {
    ("__main__.py", "main"): "a constant message when standard input holds no secret, and the log file",
    ("__main__.py", "_silence_server_loggers"): "removes the server's handlers; writes nothing",
}

WRITING_NAMES = {"print", "open", "mkstemp", "mkdtemp", "NamedTemporaryFile", "TemporaryFile", "SpooledTemporaryFile"}
WRITING_ATTRIBUTES = {"write", "writelines", "write_text", "write_bytes", "print_exc", "print_exception", "format_exc"}
LOGGING_METHODS = {"debug", "info", "warning", "warn", "error", "exception", "critical", "log"}
FORBIDDEN_MODULES = {"tempfile", "traceback", "shelve", "pickle", "sqlite3", "dbm", "logging.handlers"}


def violations(source: str, file_name: str) -> list[str]:
    """Every place in ``source`` that writes outside the closed log, as ``file:line: what``."""
    tree = ast.parse(source)
    found: list[str] = []
    enclosing: dict[ast.AST, str] = {}
    for function in ast.walk(tree):
        if isinstance(function, ast.FunctionDef | ast.AsyncFunctionDef):
            for child in ast.walk(function):
                enclosing.setdefault(child, function.name)

    for node in ast.walk(tree):
        place = f"{file_name}:{getattr(node, 'lineno', 0)}"
        if isinstance(node, ast.Import):
            found += [f"{place}: imports {a.name}" for a in node.names if a.name in FORBIDDEN_MODULES]
        elif isinstance(node, ast.ImportFrom) and node.module in FORBIDDEN_MODULES:
            found.append(f"{place}: imports from {node.module}")
        elif isinstance(node, ast.Call):
            function_name = enclosing.get(node, "")
            allowed = (file_name, function_name) in ALLOWED_WRITERS
            callee = node.func
            if isinstance(callee, ast.Attribute) and callee.attr == "event":
                for keyword in node.keywords:
                    if keyword.arg is None:
                        found.append(f"{place}: event(**...) hides which fields are written")
                    elif keyword.arg not in ALLOWED_FIELDS:
                        found.append(f"{place}: event({keyword.arg}=...) is not a field of the log")
                if node.args:
                    found.append(f"{place}: event(...) takes fields by keyword only")
            elif not allowed and isinstance(callee, ast.Name) and callee.id in WRITING_NAMES:
                found.append(f"{place}: {callee.id}(...) writes outside the log")
            elif not allowed and isinstance(callee, ast.Attribute):
                if callee.attr in WRITING_ATTRIBUTES:
                    found.append(f"{place}: .{callee.attr}(...) writes outside the log")
                elif callee.attr in LOGGING_METHODS and isinstance(callee.value, ast.Name | ast.Call):
                    found.append(f"{place}: .{callee.attr}(...) is a logging call outside the log")
    return found


def test_the_package_writes_only_through_the_closed_log() -> None:
    found: list[str] = []
    for path in sorted(PACKAGE.rglob("*.py")):
        if path.name == "logging_.py":
            continue  # the log itself: its sinks are written here, and nowhere else
        found += violations(path.read_text(encoding="utf-8"), path.relative_to(PACKAGE).as_posix())

    assert found == []


def test_the_log_module_writes_only_to_its_sinks() -> None:
    source = (PACKAGE / "logging_.py").read_text(encoding="utf-8")
    writers = [
        line.strip()
        for line in source.splitlines()
        if ".write(" in line or "print(" in line or "open(" in line
    ]

    assert writers == ["sink.write(line)"]


# --- the proof that the rule rejects, and says where --------------------------------------------

@pytest.mark.parametrize(
    ("source", "expected"),
    [
        ("log.event(reference=ref, text=body.text)", "x.py:1: event(text=...) is not a field of the log"),
        ("log.event(reference=ref, start=occurrence.start)", "x.py:1: event(start=...) is not a field of the log"),
        ("log.event(**fields)", "x.py:1: event(**...) hides which fields are written"),
        ("\n\nprint(body.text)", "x.py:3: print(...) writes outside the log"),
        ("sys.stderr.write(text)", "x.py:1: .write(...) writes outside the log"),
        ("logger.error(text)", "x.py:1: .error(...) is a logging call outside the log"),
        ("logging.getLogger('x').info(text)", "x.py:1: .info(...) is a logging call outside the log"),
        ("import tempfile", "x.py:1: imports tempfile"),
        ("from traceback import format_exc", "x.py:1: imports from traceback"),
        ("open('cache.txt', 'w')", "x.py:1: open(...) writes outside the log"),
        ("Path('cache').write_text(text)", "x.py:1: .write_text(...) writes outside the log"),
    ],
)
def test_the_rule_rejects_new_code_that_writes_and_points_at_the_line(source: str, expected: str) -> None:
    assert expected in violations(source, "x.py")


def test_the_rule_accepts_the_allowed_call() -> None:
    source = "log.event(reference=ref, chars=n, step='respond', outcome='ok')"

    assert violations(source, "x.py") == []

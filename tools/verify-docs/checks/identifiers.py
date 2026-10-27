"""Identifiers cited in `inline code` that the code does not declare."""

from __future__ import annotations

import re
from collections.abc import Iterator
from functools import cache
from pathlib import Path

from checks.references import FILE_EXTENSIONS, INLINE_CODE
from document import outside_code_blocks
from finding import Finding, Location

#: Folders whose code declares the names a document may cite.
CODE_FOLDERS = ("core", "language-service", "tools")

#: Build output, environments, caches and test fixtures: generated, third-party or deliberate
#: copies of real names for a guard to refuse, never a declaration of the system.
SKIPPED_FOLDERS = frozenset(
    {".git", ".venv", "bin", "obj", "node_modules", "__pycache__", "fixtures"}
)

#: Platform names (.NET, MSBuild, analyzer options) the documents cite and no file here declares.
PLATFORM_NAMES = frozenset(
    {
        "ConfigureAwait",
        "DiagnosticSuppressor",
        "NoWarn",
        "ResourceManager",
        "__pycache__",
        "generated_code",
        "ru_maxrss",
    }
)

_NAME = r"[A-Za-z_]\w*"

CSHARP_DECLARATION = re.compile(
    rf"\b(?:class|struct|interface|enum|record)\s+(?:class\s+|struct\s+)?({_NAME})"
    # method, property, field and constant: a type, then the name, then what opens its body
    rf"|^[ \t]*(?:[\w<>\[\],.?]+[ \t]+)+({_NAME})[ \t]*(?:<[^>\n]*>)?[ \t]*(?:\(|\{{|=>|=(?!=)|;)"
    # enum member: a lone name on its line
    rf"|^[ \t]*([A-Z]\w*)[ \t]*(?:=[^,;\n]+)?,?[ \t]*$",
    re.MULTILINE | re.ASCII,
)

#: The parameters of a primary constructor, which a positional record turns into properties.
CSHARP_PRIMARY = re.compile(
    rf"\b(?:record|class|struct)\s+(?:class\s+|struct\s+)?{_NAME}(?:<[^>]*>)?\s*\(([^)]*)\)",
    re.ASCII,
)
CSHARP_PARAMETER = re.compile(rf"({_NAME})\s*(?=,|=|$)", re.ASCII)

PYTHON_DECLARATION = re.compile(
    rf"^[ \t]*(?:async[ \t]+)?(?:def|class)[ \t]+({_NAME})"
    # module variable or constant: assigned at column zero
    rf"|^({_NAME})[ \t]*(?::[^=\n]+)?=(?!=)"
    # attribute: annotated in a class body, or assigned on self
    rf"|^[ \t]+({_NAME})[ \t]*:[ \t]*[A-Za-z_\"'\[]"
    rf"|\bself\.({_NAME})[ \t]*(?::[^=\n]+)?=(?!=)",
    re.MULTILINE | re.ASCII,
)

#: A cited snippet shaped as a name, optionally qualified (`Type.Member`) or called (`name()`).
CITED = re.compile(rf"({_NAME})((?:\.{_NAME})*)(?:\(\))?", re.ASCII)

#: What tells a code name from a word: snake_case, an inner capital, or a leading acronym.
COMPOUND = re.compile(r"_|[a-z0-9][A-Z]|[A-Z]{2}[a-z]")

EXTENSIONS = frozenset(extension.lstrip(".") for extension in FILE_EXTENSIONS)


def _code_files(root: Path) -> Iterator[Path]:
    for folder in CODE_FOLDERS:
        for directory, subfolders, files in (root / folder).walk():
            subfolders[:] = [name for name in subfolders if name not in SKIPPED_FOLDERS]
            yield from (directory / name for name in files if name.endswith((".cs", ".py")))


def _declarations(path: Path) -> Iterator[str]:
    text = path.read_text(encoding="utf-8", errors="replace")
    if path.suffix == ".py":
        yield path.parent.name if path.stem == "__init__" else path.stem
        found = PYTHON_DECLARATION.finditer(text)
    else:
        primary = CSHARP_PRIMARY.finditer(text)
        yield from (name for m in primary for name in CSHARP_PARAMETER.findall(m.group(1)))
        found = CSHARP_DECLARATION.finditer(text)
    yield from (name for match in found for name in match.groups() if name)


@cache
def declared_names(root: Path) -> frozenset[str]:
    """Every name the code under the root declares, read once per run.

    A line-based index, not a parser: a name it collects by mistake can only let a
    citation pass, never fail one. Each name also counts in camelCase, the form a
    property takes on the JSON wire.
    """
    names = {name for path in _code_files(root) for name in _declarations(path)}
    return frozenset(names | {name[:1].lower() + name[1:] for name in names})


def _cited_name(snippet: str) -> str | None:
    """The name a snippet cites, or None when it is not a code name.

    A qualified name counts only when its qualifier is compound: one qualified by a
    single word (`Core.Host`, `System.Text`) is a namespace, project or package.
    """
    match = CITED.fullmatch(snippet.strip())
    if match is None:
        return None
    qualifier, members = match.groups()
    if members and members.rsplit(".", 1)[-1] in EXTENSIONS:
        return None
    if qualifier.isupper() or COMPOUND.search(qualifier) is None:
        return None
    return qualifier + members


def check_identifiers(root: Path, path: Path, lines: list[str]) -> list[Finding]:
    """Checks that every code name cited in `inline code` is declared in the code.

    Only a compound name counts (snake_case, camelCase, PascalCase with an inner
    capital), and every part of a qualified one must be declared: a single word, a
    constant in capitals, a command, a configuration key and a path stay out, because
    nothing in their shape tells them from prose.
    """
    known = declared_names(root) | PLATFORM_NAMES
    return [
        Finding.at(Location(root, path, number), "identifiers", f"`{snippet}` não é declarado")
        for number, line in outside_code_blocks(lines)
        for snippet in INLINE_CODE.findall(line)
        if (name := _cited_name(snippet)) is not None
        and any(part not in known for part in name.split("."))
    ]

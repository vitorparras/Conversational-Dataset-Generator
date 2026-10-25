"""The graph of a lock file, read so a finding can name the path from the project down to it."""

from __future__ import annotations

import tomllib
from collections import deque
from collections.abc import Iterable, Mapping
from dataclasses import dataclass
from typing import Any, cast

UV_ROOTS = ("editable", "virtual")


@dataclass(frozen=True)
class Tree:
    """The packages a project asks for directly and what each package depends on."""

    roots: tuple[str, ...]
    edges: Mapping[str, tuple[str, ...]]


def path_to(tree: Tree, target: str) -> tuple[str, ...]:
    """The shortest chain from a root to ``target``, names compared without case; empty if none."""
    wanted = target.lower()
    queue: deque[tuple[str, ...]] = deque((root,) for root in tree.roots)
    seen = {root.lower() for root in tree.roots}
    while queue:
        path = queue.popleft()
        if path[-1].lower() == wanted:
            return path
        following = (name for name in tree.edges.get(path[-1], ()) if name.lower() not in seen)
        for name in following:
            seen.add(name.lower())
            queue.append((*path, name))
    return ()


def _uv_names(package: dict[str, Any]) -> Iterable[str]:
    """Every name the package depends on, groups and extras included."""
    groups = [*package.get("optional-dependencies", {}).values()]
    groups += [*package.get("dev-dependencies", {}).values()]
    every = [package.get("dependencies", []), *groups]
    return (str(item["name"]) for group in every for item in group)


def from_uv_lock(text: str) -> Tree:
    """The tree of a `uv.lock`; the project itself, editable or virtual, is the root."""
    packages = cast("list[dict[str, Any]]", tomllib.loads(text)["package"])
    roots = tuple(p["name"] for p in packages if set(p.get("source", {})) & set(UV_ROOTS))
    edges = {str(p["name"]): tuple(dict.fromkeys(_uv_names(p))) for p in packages}
    return Tree(roots, edges)

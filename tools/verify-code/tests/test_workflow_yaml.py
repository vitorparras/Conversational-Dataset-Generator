"""Tests that every YAML file under .github loads, with no key repeated in a mapping."""

from __future__ import annotations

import unittest
from collections.abc import Hashable
from pathlib import Path
from typing import Any, cast

import yaml

from . import support

GITHUB = support.VERIFY_CODE.parents[1] / ".github"
STEP = "jobs:\n  a:\n    steps:\n"


class StrictLoader(yaml.SafeLoader):
    """SafeLoader that refuses a key repeated in one mapping, which PyYAML would overwrite."""


def construct_unique_mapping(
    loader: StrictLoader, node: yaml.MappingNode, deep: bool = False
) -> dict[Hashable, Any]:
    loader.flatten_mapping(node)
    seen: set[Hashable] = set()
    for key_node, _ in node.value:
        key = cast(
            "Hashable",
            loader.construct_object(key_node, deep=True),  # pyright: ignore[reportUnknownMemberType]
        )
        if key in seen:
            raise yaml.constructor.ConstructorError(
                "while constructing a mapping",
                node.start_mark,
                f"key {key!r} appears twice",
                key_node.start_mark,
            )
        seen.add(key)
    return yaml.SafeLoader.construct_mapping(loader, node, deep=deep)


StrictLoader.add_constructor(
    yaml.resolver.BaseResolver.DEFAULT_MAPPING_TAG, construct_unique_mapping
)


def strict_load(text: str) -> object:
    """The document in ``text``, or a YAMLError for a syntax error or a repeated key."""
    return yaml.load(text, Loader=StrictLoader)


def yaml_files() -> list[Path]:
    """Every YAML file under .github: the workflows, the Dependabot setup and the like."""
    return sorted(path for path in GITHUB.rglob("*") if path.suffix in (".yml", ".yaml"))


class WorkflowYamlTests(unittest.TestCase):
    def test_there_are_workflows_to_check(self) -> None:
        names = {path.name for path in yaml_files()}

        self.assertIn("verify-code.yml", names)

    def test_every_yaml_file_loads_with_no_repeated_key(self) -> None:
        for path in yaml_files():
            with self.subTest(path=path.relative_to(GITHUB).as_posix()):
                self.assertIsInstance(strict_load(path.read_text(encoding="utf-8")), dict)

    def test_a_step_name_with_a_colon_and_a_space_unquoted_is_refused(self) -> None:
        planted = f"{STEP}      - name: Restaurar a cobertura (cache vazio: o passo compara)\n"

        with self.assertRaises(yaml.YAMLError):
            strict_load(planted)

    def test_the_same_step_name_quoted_loads(self) -> None:
        quoted = f"{STEP}      - name: 'Restaurar a cobertura (cache vazio: o passo compara)'\n"

        self.assertIsInstance(strict_load(quoted), dict)

    def test_a_key_repeated_in_one_mapping_is_refused(self) -> None:
        planted = "env:\n  A: 1\n  B: 2\n  A: 3\n"

        with self.assertRaises(yaml.YAMLError):
            strict_load(planted)

    def test_the_same_key_in_two_mappings_is_not_a_repetition(self) -> None:
        separate = "a:\n  name: x\nb:\n  name: y\n"

        self.assertEqual(strict_load(separate), {"a": {"name": "x"}, "b": {"name": "y"}})

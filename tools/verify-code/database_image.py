"""The name of the test database image: the registry path of the repository plus a tag that is the
hash of the files that define the image.

    python tools/verify-code/database_image.py owner/repository

The image is built once for a content and published under that name, so a job pulls it instead of
building it. Any change to a file of tools/database gives another tag; nothing else does.
"""

from __future__ import annotations

import hashlib
import sys
from pathlib import Path

REGISTRY = "ghcr.io"
IMAGE = "cdg-test-database"
DEFINITION = Path("tools") / "database"
TAG_LENGTH = 16
USAGE = 2


def content_hash(root: Path) -> str:
    """The first hex digits of the hash of the definition files, by relative path and bytes."""
    digest = hashlib.sha256()
    folder = root / DEFINITION
    for path in sorted(p for p in folder.rglob("*") if p.is_file()):
        digest.update(path.relative_to(folder).as_posix().encode("utf-8") + b"\0")
        digest.update(hashlib.sha256(path.read_bytes().replace(b"\r\n", b"\n")).digest())
    return digest.hexdigest()[:TAG_LENGTH]


def image_name(root: Path, repository: str) -> str:
    """The registry name of the image of this content; the registry wants it in lower case."""
    return f"{REGISTRY}/{repository.lower()}/{IMAGE}:{content_hash(root)}"


def main(arguments: list[str]) -> int:
    if len(arguments) != 1 or "/" not in arguments[0]:
        print("uso: database_image.py <dono>/<repositório>", file=sys.stderr)
        return USAGE
    print(image_name(Path(__file__).resolve().parents[2], arguments[0]))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

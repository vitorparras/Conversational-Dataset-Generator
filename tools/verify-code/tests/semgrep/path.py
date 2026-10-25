import os
import pathlib
import sys
from pathlib import Path


class UploadRequest:
    name: str = ""


class Request:
    query_params: dict[str, str]


def handle(body: UploadRequest, folder: Path) -> None:
    # ruleid: path-from-input-python
    open(body.name, encoding="utf-8").close()
    # ruleid: path-from-input-python
    (folder / body.name).unlink()
    # ok: path-from-input-python
    (folder / os.path.basename(body.name)).unlink()


async def download(request: Request, folder: str) -> None:
    # ruleid: path-from-input-python
    os.remove(os.path.join(folder, request.query_params["file"]))


def from_console(folder: Path) -> None:
    name = sys.stdin.readline()
    # ruleid: path-from-input-python
    Path(folder, name).unlink()
    # ok: path-from-input-python
    Path(folder, "settings.json").unlink()


def forward(body: "LateRequest", folder: Path) -> None:
    # ruleid: path-from-input-python
    (folder / body.name).unlink()
    # ruleid: path-from-input-python
    pathlib.Path(body.name).unlink()


class LateRequest:
    name: str = ""

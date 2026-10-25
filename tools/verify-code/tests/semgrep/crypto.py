import hashlib
from typing import Any

# Stand-ins for the cipher libraries, which the tools do not install; the rule matches by name.
AES: Any = None
DES: Any = None
algorithms: Any = None
modes: Any = None


def digest(data: bytes, key: bytes) -> None:
    # ruleid: weak-crypto-python
    hashlib.md5(data)
    # ruleid: weak-crypto-python
    hashlib.new("sha1", data)
    # ruleid: weak-crypto-python
    DES.new(key, DES.MODE_CBC)
    # ruleid: weak-crypto-python
    AES.new(key, AES.MODE_ECB)
    # ruleid: weak-crypto-python
    modes.ECB()
    # ruleid: weak-crypto-python
    algorithms.TripleDES(key)
    # ok: weak-crypto-python
    hashlib.sha256(data)
    # ok: weak-crypto-python
    hashlib.md5(data, usedforsecurity=False)
    # ok: weak-crypto-python
    algorithms.AES(key)

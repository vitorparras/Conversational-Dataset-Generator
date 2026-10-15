"""The startup secret, kept as a digest and compared in constant time."""

import hashlib
import hmac
from dataclasses import dataclass


@dataclass(frozen=True)
class SecretVerifier:
    """Accepts a candidate only when it equals the secret, by SHA-256 digests in constant time."""

    digest: bytes

    @classmethod
    def from_secret(cls, secret: bytes) -> "SecretVerifier":
        return cls(hashlib.sha256(secret).digest())

    def accepts(self, candidate: bytes | None) -> bool:
        """Whether ``candidate`` is the secret; a missing candidate is never accepted."""
        if candidate is None:
            return False
        return hmac.compare_digest(self.digest, hashlib.sha256(candidate).digest())

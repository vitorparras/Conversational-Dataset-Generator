"""The proof that this process holds the secret, given without ever sending the secret."""

import hashlib
import hmac
from dataclasses import dataclass, field


@dataclass(frozen=True)
class SecretProver:
    """Shows whoever asks that this process holds the secret."""

    secret: bytes = field(repr=False)

    def proof(self, nonce: bytes) -> bytes:
        """HMAC-SHA256 of ``nonce`` keyed by the secret, in lowercase hexadecimal."""
        return hmac.new(self.secret, nonce, hashlib.sha256).hexdigest().encode("ascii")

"""The startup secret admits only itself.

Written in docs/operacao/011-servico-de-linguagem.md:12-13 and in
docs/decisoes/0011-protocolo-com-o-servico-de-linguagem.md:31: the service authenticates by the
secret the core generates at startup.
"""

import pytest
from hypothesis import strategies as st

from language_service.auth import SecretVerifier
from support.property_runner import PropertyRunner

SECRETS = st.binary(min_size=1, max_size=64) | st.binary(min_size=65, max_size=300)
CANDIDATES = st.binary(max_size=300)


@pytest.mark.property
def test_the_verifier_accepts_a_candidate_only_when_it_is_the_secret() -> None:
    def property_holds(secret: bytes, candidate: bytes) -> None:
        assert SecretVerifier.from_secret(secret).accepts(candidate) == (candidate == secret)

    PropertyRunner.from_process().check(property_holds, secret=SECRETS, candidate=CANDIDATES)


@pytest.mark.property
def test_the_verifier_refuses_a_candidate_of_another_length() -> None:
    def property_holds(secret: bytes, extra: bytes) -> None:
        verifier = SecretVerifier.from_secret(secret)
        assert not verifier.accepts(secret + extra)
        assert not verifier.accepts(secret[:-1])

    PropertyRunner.from_process().check(property_holds, secret=SECRETS, extra=st.binary(min_size=1))


@pytest.mark.property
def test_the_verifier_refuses_an_absent_candidate_for_any_secret() -> None:
    def property_holds(secret: bytes) -> None:
        assert not SecretVerifier.from_secret(secret).accepts(None)

    PropertyRunner.from_process().check(property_holds, secret=SECRETS)

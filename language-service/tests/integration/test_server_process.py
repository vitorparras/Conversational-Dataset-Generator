"""Issue 29 CA-1 and CA-5 against the real server: loopback only, and a new secret on each start."""

import socket

import httpx
import pytest

from support.server import RunningService, new_secret

pytestmark = pytest.mark.integration


def _non_loopback_addresses() -> list[str]:
    """Addresses of this machine that are not 127.0.0.1, found without any network traffic."""
    found: set[str] = set()
    for info in socket.getaddrinfo(socket.gethostname(), None, family=socket.AF_INET):
        found.add(str(info[4][0]))
    return sorted(found - {"127.0.0.1"})


def test_service_answers_on_loopback_with_the_secret() -> None:
    with RunningService() as service, service.client() as client:
        assert client.get("/health").status_code == 200


def test_connection_through_any_other_address_is_not_established() -> None:
    # 127.0.0.2 is also loopback, but it is not the interface the service bound.
    addresses = ["127.0.0.2", *_non_loopback_addresses()]
    with RunningService() as service:
        for address in addresses:
            with pytest.raises(httpx.TransportError):
                httpx.get(
                    f"http://{address}:{service.port}/health",
                    headers={"X-Service-Secret": service.secret},
                    timeout=3.0,
                )


def test_a_restarted_service_refuses_the_previous_secret() -> None:
    with RunningService(secret=new_secret()) as first:
        old_secret = first.secret
    with RunningService(secret=new_secret()) as second:
        assert second.secret != old_secret
        with second.client(secret=old_secret) as stale:
            assert stale.get("/health").status_code == 401
        with second.client() as current:
            assert current.get("/health").status_code == 200


def test_the_secret_is_not_in_the_output() -> None:
    with RunningService() as service:
        pass
    assert service.secret not in service.stdout_text
    assert service.secret not in service.stderr_text


def test_service_exits_when_the_stream_from_the_core_closes() -> None:
    # What the system does to the stream when the core dies, however it dies.
    service = RunningService()
    try:
        with service:
            service.close_stdin()
            code = service.wait_for_exit(timeout=15)
    finally:
        service.dispose()

    assert code is not None, "the service stayed up after the core's stream closed"
    assert code == 0

"""Issue 29 CA-1 by construction: the host is a constant and no path leads to another one."""

import inspect
import io
import os
import threading

import pytest

from language_service import __main__ as entry


def test_the_host_is_the_loopback_constant() -> None:
    assert entry.LOOPBACK_HOST == "127.0.0.1"


def test_the_server_configuration_binds_the_constant() -> None:
    config = entry.build_config(lambda *_: None, 8123)  # type: ignore[arg-type,return-value]

    assert config.host == "127.0.0.1"
    assert config.port == 8123
    assert config.access_log is False
    assert config.should_reload is False


def test_no_function_of_the_entry_point_takes_a_host() -> None:
    for function in (entry.build_config, entry.parse_arguments, entry.main):
        assert "host" not in inspect.signature(function).parameters


@pytest.mark.parametrize("flag", ["--host", "--bind", "--host=0.0.0.0", "--ho"])
def test_command_line_has_no_host_option(flag: str) -> None:
    with pytest.raises(SystemExit) as exit_info:
        entry.parse_arguments(["--port", "8123", flag, "0.0.0.0"])

    assert exit_info.value.code == 2


@pytest.mark.parametrize("port", ["0", "65536", "-1", "abc"])
def test_port_must_be_a_real_port(port: str) -> None:
    with pytest.raises(SystemExit):
        entry.parse_arguments(["--port", port])


def test_port_is_required() -> None:
    with pytest.raises(SystemExit):
        entry.parse_arguments([])


def test_secret_is_the_first_line_without_its_break() -> None:
    secret = b"x" * 64

    assert entry.read_secret(io.BytesIO(secret + b"\r\nrest")) == secret


@pytest.mark.parametrize("content", [b"", b"\n", b"short\n", b"y" * 5000])
def test_missing_or_malformed_secret_is_refused(content: bytes) -> None:
    with pytest.raises(entry.SecretNotProvidedError):
        entry.read_secret(io.BytesIO(content))


def test_main_exits_with_usage_code_when_stdin_holds_no_secret(capsys: pytest.CaptureFixture[str]) -> None:
    code = entry.main(["--port", "8123"], stdin=io.BytesIO(b""))

    assert code == entry.EXIT_USAGE
    assert "segredo" in capsys.readouterr().err


@pytest.mark.skipif(not hasattr(os, "fork"), reason="memory dumps are limited with resource.setrlimit, which exists on POSIX only")
def test_core_dumps_are_disabled_where_the_system_allows() -> None:
    import resource

    before = resource.getrlimit(resource.RLIMIT_CORE)
    try:
        assert entry.disable_core_dumps() is True
        assert resource.getrlimit(resource.RLIMIT_CORE) == (0, 0)
    finally:
        resource.setrlimit(resource.RLIMIT_CORE, before)


@pytest.mark.skipif(hasattr(os, "fork"), reason="on POSIX the function acts, and the test above proves it")
def test_core_dump_limit_reports_that_it_cannot_act_without_posix() -> None:
    assert entry.disable_core_dumps() is False


def test_the_watch_reports_the_end_of_the_stream() -> None:
    gone = threading.Event()

    entry.watch_parent(io.BytesIO(b""), gone.set).join(timeout=5)

    assert gone.is_set()


def test_the_watch_waits_for_the_end_and_does_not_fire_before() -> None:
    read_end, write_end = os.pipe()
    gone = threading.Event()
    with os.fdopen(read_end, "rb") as stream:
        thread = entry.watch_parent(stream, gone.set)
        assert not gone.wait(timeout=0.5), "the watch fired while the writer was still open"

        os.close(write_end)
        thread.join(timeout=5)

        assert gone.is_set()


def test_the_command_line_refusal_is_in_portuguese(capsys: pytest.CaptureFixture[str]) -> None:
    with pytest.raises(SystemExit):
        entry.parse_arguments([])

    assert "argumentos inválidos" in capsys.readouterr().err

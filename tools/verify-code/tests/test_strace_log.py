"""Tests for the reading of the strace output, with traces written by hand."""

from __future__ import annotations

import unittest

from steps import strace_log

HOST = "13<Core.Host>"
OTHER = "21<zqx-helper>"
BIND = (
    f"{HOST} bind(173, {{sa_family=AF_INET, sin_port=htons(5000), "
    'sin_addr=inet_addr("127.0.0.1")}, 16) = 0'
)
UDP_PROBE = f"{HOST} socket(AF_INET6, SOCK_DGRAM|SOCK_CLOEXEC, IPPROTO_IP) = 136"
NETLINK = f"{HOST} socket(AF_NETLINK, SOCK_RAW|SOCK_CLOEXEC, NETLINK_ROUTE) = 9"
UNIX = f'{HOST} connect(5, {{sa_family=AF_UNIX, sun_path="/zqx/socket"}}, 110) = -1 ENOENT'
RESOLVER_READ = f'{HOST} openat(AT_FDCWD, "/etc/resolv.conf", O_RDONLY|O_CLOEXEC) = 7'
RESOLVER_MISSING = (
    f'{HOST} openat(AT_FDCWD, "/etc/resolv.conf", O_RDONLY) = -1 ENOENT (No such file)'
)


def connect(address: str, port: int, who: str = HOST, family: str = "AF_INET") -> str:
    """A connect to an IPv4 address as strace prints it."""
    sockaddr = f'sa_family={family}, sin_port=htons({port}), sin_addr=inet_addr("{address}")'
    return f"{who} connect(9, {{{sockaddr}}}, 16) = -1 EINPROGRESS (Operation now in progress)"


def connect6(address: str, port: int, call: str = "connect") -> str:
    """A call to an IPv6 address as strace prints it."""
    sockaddr = (
        f"sa_family=AF_INET6, sin6_port=htons({port}), sin6_flowinfo=htonl(0), "
        f'inet_pton(AF_INET6, "{address}", &sin6_addr), sin6_scope_id=0'
    )
    return f"{HOST} {call}(9, {{{sockaddr}}}, 28) = 0"


def sendto(address: str, port: int) -> str:
    """A datagram to an IPv4 address as strace prints it."""
    sockaddr = f'sa_family=AF_INET, sin_port=htons({port}), sin_addr=inet_addr("{address}")'
    return f'{HOST} sendto(9, "\\1\\0\\1", 3, MSG_NOSIGNAL, {{{sockaddr}}}, 16) = 3'


def trace(*lines: str) -> str:
    return "\n".join(lines) + "\n"


class RefusalsTests(unittest.TestCase):
    def test_a_start_that_only_binds_and_probes_sockets_on_the_loopback_is_clean(self) -> None:
        text = trace(UDP_PROBE, NETLINK, BIND, connect("127.0.0.1", 5000), UNIX)

        self.assertEqual(strace_log.refusals(text), ())

    def test_a_connection_to_an_address_outside_the_loopback_is_named(self) -> None:
        text = trace(BIND, connect("192.0.2.7", 443))

        self.assertEqual(
            strace_log.refusals(text),
            ("conexão fora do loopback: connect para 192.0.2.7:443 (processo Core.Host, pid 13)",),
        )

    def test_the_process_that_made_the_call_is_the_one_named(self) -> None:
        text = trace(connect("192.0.2.7", 443, OTHER))

        self.assertIn("(processo zqx-helper, pid 21)", strace_log.refusals(text)[0])

    def test_a_datagram_to_an_address_outside_the_loopback_is_named(self) -> None:
        text = trace(sendto("192.0.2.9", 9999))

        self.assertEqual(
            strace_log.refusals(text),
            ("conexão fora do loopback: sendto para 192.0.2.9:9999 (processo Core.Host, pid 13)",),
        )

    def test_a_connection_to_an_ipv6_address_outside_the_loopback_is_named(self) -> None:
        text = trace(connect6("2001:db8::1", 443))

        self.assertIn("connect para [2001:db8::1]:443", strace_log.refusals(text)[0])

    def test_an_ipv6_loopback_connection_is_clean(self) -> None:
        self.assertEqual(strace_log.refusals(trace(connect6("::1", 5000))), ())

    def test_an_ipv4_mapped_loopback_connection_is_clean(self) -> None:
        self.assertEqual(strace_log.refusals(trace(connect6("::ffff:127.0.0.1", 5000))), ())

    def test_an_ipv4_mapped_address_outside_the_loopback_is_named(self) -> None:
        text = trace(connect6("::ffff:192.0.2.7", 443))

        self.assertIn("conexão fora do loopback", strace_log.refusals(text)[0])

    def test_a_message_sent_to_an_address_outside_the_loopback_is_named(self) -> None:
        text = trace(connect6("2001:db8::1", 443, call="sendmsg"))

        self.assertIn("sendmsg para [2001:db8::1]:443", strace_log.refusals(text)[0])

    def test_a_query_to_a_name_server_outside_the_loopback_is_both_a_connection_and_a_dns_query(
        self,
    ) -> None:
        text = trace(sendto("192.0.2.53", 53))

        self.assertEqual(
            strace_log.refusals(text),
            (
                "conexão fora do loopback: sendto para 192.0.2.53:53 (processo Core.Host, pid 13)",
                "consulta de DNS: sendto para 192.0.2.53:53 (processo Core.Host, pid 13)",
            ),
        )

    def test_a_query_to_a_name_server_on_the_loopback_is_still_a_dns_query(self) -> None:
        text = trace(sendto("127.0.0.53", 53))

        self.assertEqual(
            strace_log.refusals(text),
            ("consulta de DNS: sendto para 127.0.0.53:53 (processo Core.Host, pid 13)",),
        )

    def test_a_datagram_socket_after_the_resolver_file_was_read_is_a_dns_query(self) -> None:
        text = trace(BIND, RESOLVER_READ, UDP_PROBE)

        self.assertEqual(
            strace_log.refusals(text),
            (f"consulta de DNS: socket UDP depois de ler /etc/resolv.conf: {UDP_PROBE}",),
        )

    def test_a_datagram_socket_before_the_resolver_file_is_read_is_clean(self) -> None:
        self.assertEqual(strace_log.refusals(trace(UDP_PROBE, RESOLVER_READ)), ())

    def test_a_resolver_file_that_does_not_exist_is_not_a_read(self) -> None:
        self.assertEqual(strace_log.refusals(trace(RESOLVER_MISSING, UDP_PROBE)), ())

    def test_a_call_that_was_interrupted_still_names_its_destination(self) -> None:
        line = connect("192.0.2.7", 443).split(" = ", 1)[0] + " <unfinished ...>"
        resumed = f"{HOST} <... connect resumed>) = -1 EINPROGRESS (Operation now in progress)"

        found = strace_log.refusals(trace(line, resumed))

        self.assertEqual(len(found), 1)

    def test_the_same_attempt_made_again_is_named_once(self) -> None:
        text = trace(connect("192.0.2.7", 443), connect("192.0.2.7", 443))

        self.assertEqual(len(strace_log.refusals(text)), 1)

    def test_a_trace_with_nothing_in_it_is_clean_and_sees_nothing(self) -> None:
        self.assertEqual((strace_log.refusals(""), strace_log.seen("")), ((), ()))


class SeenTests(unittest.TestCase):
    def test_each_destination_is_listed_once_with_its_call(self) -> None:
        text = trace(BIND, connect("127.0.0.1", 5000), connect("127.0.0.1", 5000), UNIX)

        self.assertEqual(strace_log.seen(text), ("bind 127.0.0.1:5000", "connect 127.0.0.1:5000"))


if __name__ == "__main__":
    unittest.main()

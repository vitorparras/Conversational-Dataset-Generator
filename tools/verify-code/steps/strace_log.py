"""Reads what strace saw of the host starting and names every attempt to reach past the loopback.

The trace comes from `strace -f --decode-pids=comm -e trace=network,openat`, so each line starts
with the pid and the name of the process: `13<Core.Host> connect(3, {sa_family=AF_INET, ...}, 16)`.
Three things are refused: a connection or datagram to an address outside 127.0.0.0/8 and ::1; any
destination on the DNS port, loopback included; and a datagram socket opened after the resolver
configuration was read. A unix socket or a netlink socket is local and never counts.
"""

from __future__ import annotations

import ipaddress
import re
from dataclasses import dataclass

DNS_PORT = 53
RESOLVER_FILE = "/etc/resolv.conf"
SENDING = frozenset({"connect", "sendto", "sendmsg", "sendmmsg"})
INTERNET = frozenset({"AF_INET", "AF_INET6"})
LINE = re.compile(r"^(?P<pid>\d+)<(?P<process>[^>]*)> (?P<call>\w+)\((?P<arguments>.*)$")
FAMILY = re.compile(r"sa_family=(AF_\w+)")
PORT = re.compile(r"sin6?_port=htons\((\d+)\)")
ADDRESS = re.compile(r'inet_addr\("([^"]+)"\)|inet_pton\(AF_INET6, "([^"]+)"')
OPENED = re.compile(r'openat\(.*"' + re.escape(RESOLVER_FILE) + r'".*\) = \d+$')
DATAGRAM = re.compile(r"> socket\(AF_INET6?, SOCK_DGRAM")


@dataclass(frozen=True)
class Attempt:
    """One call that names a destination: who made it, which call, and where it was aimed."""

    pid: int
    process: str
    call: str
    address: str
    port: int | None

    @property
    def destination(self) -> str:
        host = f"[{self.address}]" if ":" in self.address else self.address
        return host if self.port is None else f"{host}:{self.port}"

    @property
    def leaves_the_machine(self) -> bool:
        return self.call in SENDING and not is_loopback(self.address)

    @property
    def asks_dns(self) -> bool:
        return self.call in SENDING and self.port == DNS_PORT

    def describe(self) -> str:
        return f"{self.call} para {self.destination} (processo {self.process}, pid {self.pid})"


def is_loopback(address: str) -> bool:
    """Whether the address is in 127.0.0.0/8 or is ::1, mapped into IPv4 or not."""
    parsed = ipaddress.ip_address(address)
    if isinstance(parsed, ipaddress.IPv6Address) and parsed.ipv4_mapped is not None:
        return parsed.ipv4_mapped.is_loopback
    return parsed.is_loopback


def destination_of(arguments: str) -> tuple[str, int | None] | None:
    """The internet address and port the arguments name, or None for a unix socket or no address."""
    family = FAMILY.search(arguments)
    address = ADDRESS.search(arguments)
    if family is None or family.group(1) not in INTERNET or address is None:
        return None
    port = PORT.search(arguments)
    return (address.group(1) or address.group(2), int(port.group(1)) if port else None)


def attempts(trace: str) -> tuple[Attempt, ...]:
    """Every call of the trace that names an internet destination, in the order it was made."""
    found: list[Attempt] = []
    for line in trace.splitlines():
        parsed = LINE.match(line)
        destination = destination_of(parsed.group("arguments")) if parsed else None
        if parsed and destination:
            pid, process = int(parsed.group("pid")), parsed.group("process")
            found.append(Attempt(pid, process, parsed.group("call"), *destination))
    return tuple(found)


def datagram_after_resolver(trace: str) -> str | None:
    """The first datagram socket opened after the resolver file was read, as the line shows it."""
    read = False
    for line in trace.splitlines():
        read = read or OPENED.search(line) is not None
        if read and DATAGRAM.search(line):
            return line
    return None


def refusals(trace: str) -> tuple[str, ...]:
    """What the start must not do, once each, named by call, destination and process."""
    outside = tuple(
        f"conexão fora do loopback: {a.describe()}" for a in attempts(trace) if a.leaves_the_machine
    )
    dns = tuple(f"consulta de DNS: {a.describe()}" for a in attempts(trace) if a.asks_dns)
    resolver = datagram_after_resolver(trace)
    after = (f"consulta de DNS: socket UDP depois de ler {RESOLVER_FILE}: {resolver}",)
    return tuple(dict.fromkeys((*outside, *dns, *(after if resolver else ()))))


def seen(trace: str) -> tuple[str, ...]:
    """The distinct destinations the trace names, for the report of a start that passed."""
    return tuple(dict.fromkeys(f"{a.call} {a.destination}" for a in attempts(trace)))

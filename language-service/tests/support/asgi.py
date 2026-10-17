"""Doubles for a raw ASGI exchange: a request that records what the gate did with it."""

from __future__ import annotations

from dataclasses import dataclass, field

from starlette.types import Message, Receive, Scope, Send

from support.synthetic import MARKER


@dataclass
class Exchange:
    """One HTTP request as the ASGI server hands it over, counting every read of its body."""

    body_reads: int = 0
    application_calls: int = 0
    sent: list[Message] = field(default_factory=list[Message])

    @staticmethod
    def scope(headers: tuple[tuple[bytes, bytes], ...] = ()) -> Scope:
        return {"type": "http", "method": "POST", "path": "/health", "headers": list(headers)}

    async def receive(self) -> Message:
        self.body_reads += 1
        return {"type": "http.request", "body": MARKER.encode("ascii"), "more_body": False}

    async def send(self, message: Message) -> None:
        self.sent.append(message)

    async def application(self, scope: Scope, receive: Receive, send: Send) -> None:
        self.application_calls += 1

    @property
    def status(self) -> int | None:
        return next(
            (int(m["status"]) for m in self.sent if m["type"] == "http.response.start"), None
        )

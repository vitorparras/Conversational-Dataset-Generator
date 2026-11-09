"""Retries of what the CI downloads, and the summary of the download that did not come.

A tool, a package, a model or a base image comes from a server the CI does not control, and a 504
from it costs a whole run of the serial queue. A command that downloads is tried again, a few times
and with a wait that grows, when its output tells a failure of the network; any other failure is
returned at once, so a test or a build that fails is never run again. When the last try fails, or a
try that failed was repaired by the next, the job summary says which URL and which code.
"""

from __future__ import annotations

import re
import time
from collections.abc import Callable, Sequence
from dataclasses import dataclass
from pathlib import Path

from steps.common import CommandOutcome, RunCommand
from steps.coverage_by_area import summarize

WAITS = (10.0, 30.0, 90.0)
ATTEMPTS = len(WAITS) + 1
SHOWN_LINES = 5
DOWNLOADERS = frozenset({"uv", "pip", "pip3", "pip-audit", "curl", "wget", "trivy", "pwsh", "gh"})
RESTORING = frozenset({"build", "publish"})
NETWORK = re.compile(
    "|".join(
        (
            r"gateway time-?out",
            r"bad gateway",
            r"service unavailable",
            r"too many requests",
            r"timed? ?out",
            r"connection (?:reset|refused|aborted|closed)",
            r"could not resolve",
            r"temporary failure in name resolution",
            r"name or service not known",
            r"network is unreachable",
            r"unexpected eof",
            r"ssl[: ].*(?:error|handshake|eof)",
            r"tls handshake",
            r"remotedisconnected",
            r"econnreset",
            r"etimedout",
            r"failed to fetch",
            r"unable to connect",
            r"error sending request",
            r"an error occurred while sending the request",
            r"nu1301",
            r"unable to load the service index",
        )
    ),
    re.IGNORECASE,
)
STATUS = re.compile(r"\b(429|50[0-4])\b")
STATUS_CONTEXT = re.compile(r"\bhttp\b|status|response|gateway|returned", re.IGNORECASE)
CODE = re.compile(r"\b([45]\d\d)\b")
CURL_CODE = re.compile(r"curl: \((\d+)\)")
URL = re.compile(r"https?://[^\s\"'<>)\],]+")
Sleep = Callable[[float], None]
Publish = Callable[[str], None]


@dataclass(frozen=True)
class Evidence:
    """What a failed try says about the download: the URLs, the codes and the lines that told it."""

    urls: tuple[str, ...]
    codes: tuple[str, ...]
    lines: tuple[str, ...]


def is_download(command: Sequence[str]) -> bool:
    """Whether the command brings something from the network, by the tool and the verb it calls."""
    words = [Path(word).name for word in command[:3]]
    if not words:
        return False
    if words[0] in DOWNLOADERS or "pip" in words:
        return True
    if words[0] == "docker":
        return len(words) > 1 and words[1] in {"build", "pull"}
    return words[0] == "dotnet" and _dotnet_downloads(words, command)


def _dotnet_downloads(words: list[str], command: Sequence[str]) -> bool:
    """A restore, a restore of the tools, or a build or a publish that is not told to skip it."""
    verb = words[1:2]
    return verb == ["restore"] or words[1:3] == ["tool", "restore"] or _restores(command, verb)


def _restores(command: Sequence[str], verb: list[str]) -> bool:
    """A build or a publish restores by itself unless it is told not to."""
    return bool(verb) and verb[0] in RESTORING and "--no-restore" not in command


def failure_lines(text: str) -> tuple[str, ...]:
    """The lines of the output that tell a failure of the network."""
    return tuple(line.strip() for line in text.splitlines() if _tells_network(line))


def _tells_network(line: str) -> bool:
    return bool(NETWORK.search(line)) or bool(STATUS.search(line) and STATUS_CONTEXT.search(line))


def evidence(command: Sequence[str], text: str) -> Evidence:
    """The URLs of the command and of the output, and the codes of the lines of the failure."""
    lines = failure_lines(text)
    seen = [*URL.findall(" ".join(command)), *(u for line in lines for u in URL.findall(line))]
    codes = [
        f"HTTP {m}" for line in lines for m in CODE.findall(line) if STATUS_CONTEXT.search(line)
    ]
    codes += [f"curl ({m})" for m in CURL_CODE.findall(text)]
    if not codes:
        codes = [_phrase(line) for line in lines]
    return Evidence(_once(seen), _once(codes), _once(lines))


def _phrase(line: str) -> str:
    found = NETWORK.search(line)
    return found.group(0).lower() if found else line[:80]


def _once(items: Sequence[str]) -> tuple[str, ...]:
    return tuple(dict.fromkeys(items))


def report(command: Sequence[str], found: Evidence, tries: int, repaired: bool) -> str:
    """The Markdown for the job summary: what was downloaded, from where and the code it got."""
    title = "Download repetido e concluído" if repaired else "Download que caiu"
    urls = ", ".join(found.urls) or "nenhuma URL no comando nem na saída"
    lines = [
        f"### {title}: `{' '.join(command[:4])}`",
        "",
        f"- comando: `{' '.join(command)}`",
        f"- URL: {urls}",
        f"- código: {', '.join(found.codes) or 'não informado'}",
        f"- tentativas: {tries} de {ATTEMPTS}",
        *(f"- saída: `{line[:200]}`" for line in found.lines[:SHOWN_LINES]),
        "",
    ]
    return "\n".join(lines)


def repeat(
    attempt: Callable[[], CommandOutcome],
    command: Sequence[str],
    *,
    sleep: Sleep = time.sleep,
    publish: Publish = summarize,
) -> CommandOutcome:
    """Runs ``attempt`` again while it fails with a network failure, waiting longer each time."""
    outcome = attempt()
    found: Evidence | None = None
    tries = 1
    for wait in WAITS:
        if outcome.ok or not failure_lines(outcome.text):
            break
        found = evidence(command, outcome.text)
        sleep(wait)
        outcome = attempt()
        tries += 1
    if not outcome.ok and failure_lines(outcome.text):
        found = evidence(command, outcome.text)
    if found is not None:
        publish(report(command, found, tries, repaired=outcome.ok))
    return outcome


def retrying(
    run: RunCommand, *, sleep: Sleep = time.sleep, publish: Publish = summarize
) -> RunCommand:
    """``run`` that tries a download again; any other command goes through as it is."""

    def run_again(command: Sequence[str], cwd: Path) -> CommandOutcome:
        if not is_download(command):
            return run(command, cwd)
        return repeat(lambda: run(command, cwd), command, sleep=sleep, publish=publish)

    return run_again

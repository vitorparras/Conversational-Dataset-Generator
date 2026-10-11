# language_service

Local language service. It receives text and returns pointers (code point offsets, kind, part of
speech, syntactic role), never transformed text. It binds to 127.0.0.1 only and authenticates every
request with a secret the core writes to its standard input.

Run the tests with `uv sync --frozen` and `uv run pytest` inside this folder.

The core keeps the standard input open after the secret; when it closes (the core is gone, however it
died) the service stops. `LanguageService:Arguments` splits on `;`, so it cannot express an argument
that contains one.

"""The error of a log call that names a key outside the allowed list."""


class UnknownLogFieldError(ValueError):
    """A programming error: a log call carried a key that is not a ``LogField``."""

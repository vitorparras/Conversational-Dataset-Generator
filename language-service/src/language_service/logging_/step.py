"""Where in the analysis a request was when it failed."""

from enum import StrEnum


class Step(StrEnum):
    """The stages of an analysis, named so that a failure points at one without quoting input."""

    RECOGNIZE = "recognize"
    RESPOND = "respond"

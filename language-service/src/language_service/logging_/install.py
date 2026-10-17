"""Putting the fixed-field handler where every record of the process goes through it."""

import logging
from typing import TextIO

from language_service.logging_.fixed_field_handler import FixedFieldHandler


def install_root_handler(stream: TextIO) -> None:
    """Makes ``stream`` the only destination of the process's log, rendered by the fixed format."""
    root = logging.getLogger()
    root.handlers[:] = [FixedFieldHandler(stream)]
    root.setLevel(logging.INFO)

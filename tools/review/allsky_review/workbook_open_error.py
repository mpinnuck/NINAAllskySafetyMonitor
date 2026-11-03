"""Raised when review.xlsx can't be saved because Excel has it open."""
from .traceability import requirement


@requirement("REQ-62")
class WorkbookOpenError(Exception):
    pass

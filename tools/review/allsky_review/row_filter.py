"""Which review rows are shown."""
from __future__ import annotations

from enum import Enum

from .traceability import requirement


@requirement("REQ-62")
class RowFilter(Enum):
    ALL = "All frames"
    UNREVIEWED = "Not reviewed yet"
    MONITOR_CLOUD = "Monitor said Cloud"
    MONITOR_CLEAR = "Monitor said Clear"
    DISAGREE = "Verdicts disagree"

    def matches(self, row) -> bool:
        if self is RowFilter.UNREVIEWED:
            return row.my_verdict is None
        if self is RowFilter.MONITOR_CLOUD:
            return row.verdict == "Cloud"
        if self is RowFilter.MONITOR_CLEAR:
            return row.verdict == "Clear"
        if self is RowFilter.DISAGREE:
            return row.agrees is False
        return True

    def following(self) -> "RowFilter":
        """The next filter in the list, wrapping round (the F key)."""
        members = list(RowFilter)
        return members[(members.index(self) + 1) % len(members)]

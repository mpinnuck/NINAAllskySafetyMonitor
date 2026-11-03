"""Agreement counts, the same as the Summary sheet of review.xlsx."""
from __future__ import annotations

from dataclasses import dataclass
from typing import Iterable, Optional

from .traceability import requirement


@requirement("REQ-62")
@dataclass(frozen=True)
class Tally:
    total: int
    reviewed: int
    agree: int
    disagree: int
    false_cloud: int
    missed_cloud: int

    @property
    def agreement(self) -> Optional[float]:
        decided = self.agree + self.disagree
        return self.agree / decided if decided else None

    @staticmethod
    def of(rows: Iterable) -> "Tally":
        rows = list(rows)
        return Tally(
            total=len(rows),
            reviewed=sum(1 for r in rows if r.my_verdict is not None),
            agree=sum(1 for r in rows if r.agrees is True),
            disagree=sum(1 for r in rows if r.agrees is False),
            false_cloud=sum(1 for r in rows if r.verdict == "Cloud" and r.my_verdict == "Clear"),
            missed_cloud=sum(1 for r in rows if r.verdict == "Clear" and r.my_verdict == "Cloud"),
        )

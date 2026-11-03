"""One row of the Review sheet of review.xlsx."""
from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Optional

from .traceability import requirement

MY_VERDICTS = ("Clear", "Cloud", "Unsure")


@requirement("REQ-62")
@dataclass
class ReviewRow:
    excel_row: int
    night: str
    local_time: str
    target: str
    verdict: str
    stars_found: Optional[int]
    stars_expected: Optional[int]
    star_fraction: Optional[float]
    brightness: Optional[float]
    uniformity: Optional[float]
    picked_because: str
    image: Optional[Path]
    my_verdict: Optional[str] = None

    @property
    def agrees(self) -> Optional[bool]:
        """Same rule as the Agrees column: blank until a Clear or Cloud verdict is entered."""
        if self.my_verdict not in ("Clear", "Cloud"):
            return None
        return self.my_verdict == self.verdict

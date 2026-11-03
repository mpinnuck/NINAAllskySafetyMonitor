"""Reads the Review sheet of review.xlsx and writes the imager's verdicts back to it."""
from __future__ import annotations

import os
import re
from datetime import date, datetime
from pathlib import Path
from typing import Dict, List, Optional

from openpyxl import load_workbook

from .review_row import MY_VERDICTS, ReviewRow
from .traceability import requirement
from .workbook_open_error import WorkbookOpenError

WORKBOOK = "review.xlsx"
SHEET = "Review"
HEADINGS = {
    "night": "Night",
    "local_time": "Local time",
    "target": "Target",
    "verdict": "Verdict",
    "stars_found": "Stars found",
    "stars_expected": "Stars expected",
    "star_fraction": "Star fraction",
    "brightness": "Relative brightness",
    "uniformity": "Non-uniformity ratio",
    "picked_because": "Picked because",
    "image": "Image",
    "my_verdict": "My verdict",
}
_LINK = re.compile(r'HYPERLINK\(\s*"([^"]+)"', re.IGNORECASE)


@requirement("REQ-61", "REQ-62")
class ReviewWorkbook:
    """The review.xlsx written by the harness. Columns are found by their headings. Only the My verdict
    column is written, so the drop-down, the Agrees formulas and the Summary sheet stay as the harness
    wrote them; Excel recalculates them when the workbook is opened."""

    def __init__(self, run_folder) -> None:
        self.run_folder = Path(run_folder).expanduser()
        self.path = self.run_folder / WORKBOOK
        if not self.path.is_file():
            raise FileNotFoundError(f"No {WORKBOOK} in {self.run_folder}: run the harness replay first.")
        self._book = load_workbook(self.path)
        if SHEET not in self._book.sheetnames:
            raise ValueError(f"{self.path} has no {SHEET} sheet.")
        self._sheet = self._book[SHEET]
        self._columns = self._find_columns()
        self.unsaved = False

    def load(self) -> List[ReviewRow]:
        rows = []
        for r in range(2, self._sheet.max_row + 1):
            target = self._value(r, "target")
            if target in (None, ""):
                continue
            my = self._value(r, "my_verdict")
            rows.append(ReviewRow(
                excel_row=r,
                night=_text(self._value(r, "night"), "%Y-%m-%d"),
                local_time=_text(self._value(r, "local_time"), "%Y-%m-%d %H:%M:%S"),
                target=str(target),
                verdict=str(self._value(r, "verdict") or ""),
                stars_found=_int(self._value(r, "stars_found")),
                stars_expected=_int(self._value(r, "stars_expected")),
                star_fraction=_float(self._value(r, "star_fraction")),
                brightness=_float(self._value(r, "brightness")),
                uniformity=_float(self._value(r, "uniformity")),
                picked_because=str(self._value(r, "picked_because") or ""),
                image=self._image(r),
                my_verdict=my if my in MY_VERDICTS else None,
            ))
        return rows

    def set_verdict(self, row: ReviewRow) -> None:
        """Puts the row's My verdict in the workbook (in memory until save)."""
        if row.my_verdict is not None and row.my_verdict not in MY_VERDICTS:
            raise ValueError(f"Verdict must be one of {', '.join(MY_VERDICTS)}.")
        self._sheet.cell(row=row.excel_row, column=self._columns["my_verdict"]).value = row.my_verdict
        self.unsaved = True

    def is_open_in_excel(self) -> bool:
        """Excel keeps a lock file named ~$review.xlsx beside a workbook it has open."""
        return (self.run_folder / ("~$" + WORKBOOK)).exists()

    def save(self) -> None:
        """Saves to a temporary file, then replaces review.xlsx, so a failed save can't damage it."""
        if not self.unsaved:
            return
        if self.is_open_in_excel():
            raise WorkbookOpenError(f"{WORKBOOK} is open in Excel. Close it there and the verdicts will be saved.")
        temp = self.path.with_name(WORKBOOK + ".saving")
        self._book.calculation.fullCalcOnLoad = True  # openpyxl keeps no cached Agrees values; make Excel recalculate
        try:
            self._book.save(temp)
            os.replace(temp, self.path)
        except PermissionError as e:
            raise WorkbookOpenError(f"{WORKBOOK} can't be written ({e.strerror}). Is it open in Excel?") from e
        finally:
            if temp.exists():
                temp.unlink()
        self.unsaved = False

    def _find_columns(self) -> Dict[str, int]:
        found = {}
        for cell in self._sheet[1]:
            if cell.value is not None:
                found[str(cell.value).strip()] = cell.column
        missing = [h for h in HEADINGS.values() if h not in found]
        if missing:
            raise ValueError(f"{self.path} Review sheet is missing the column(s): {', '.join(missing)}.")
        return {key: found[heading] for key, heading in HEADINGS.items()}

    def _value(self, row: int, key: str):
        return self._sheet.cell(row=row, column=self._columns[key]).value

    def _image(self, row: int) -> Optional[Path]:
        cell = self._sheet.cell(row=row, column=self._columns["image"])
        link = None
        if isinstance(cell.value, str):
            m = _LINK.search(cell.value)
            link = m.group(1) if m else (cell.value if not cell.value.startswith("=") else None)
        if link is None and cell.hyperlink is not None:
            link = cell.hyperlink.target
        return self.run_folder / link if link else None


def _text(value, fmt: str) -> str:
    if isinstance(value, (datetime, date)):
        return value.strftime(fmt)
    return "" if value is None else str(value)


def _float(value) -> Optional[float]:
    try:
        return None if value in (None, "") else float(value)
    except (TypeError, ValueError):
        return None


def _int(value) -> Optional[int]:
    f = _float(value)
    return None if f is None else int(round(f))

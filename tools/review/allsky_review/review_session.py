"""Where the imager is in the review: the current row, the filter, verdicts and the running counts."""
from __future__ import annotations

from typing import List, Optional, Tuple

from .review_row import MY_VERDICTS, ReviewRow
from .row_filter import RowFilter
from .tally import Tally
from .traceability import requirement
from .workbook_open_error import WorkbookOpenError


@requirement("REQ-62")
class ReviewSession:
    """Steps through the rows that match the filter, in the order of the Review sheet. A verdict is saved
    at once; if the workbook can't be saved it stays in memory and is saved with the next one."""

    def __init__(self, workbook, rows: Optional[List[ReviewRow]] = None) -> None:
        self.workbook = workbook
        self.rows: List[ReviewRow] = rows if rows is not None else workbook.load()
        self.filter = RowFilter.ALL
        self.save_error: Optional[str] = None
        first = next((i for i, r in enumerate(self.rows) if r.my_verdict is None), 0)
        self._index: Optional[int] = first if self.rows else None

    @property
    def current(self) -> Optional[ReviewRow]:
        return None if self._index is None else self.rows[self._index]

    def position(self) -> Tuple[int, int]:
        """(n, m): the current row is the n-th of the m rows that match the filter (n is 0 when none)."""
        shown = self._shown()
        n = shown.index(self._index) + 1 if self._index in shown else 0
        return n, len(shown)

    def shown_rows(self) -> List[ReviewRow]:
        return [self.rows[i] for i in self._shown()]

    def go_to(self, row: ReviewRow) -> bool:
        """Makes row current when it matches the filter."""
        i = next((i for i in self._shown() if self.rows[i] is row), None)
        return self._move_to(i)

    def restore(self, excel_row: Optional[int], filter_name: Optional[str]) -> None:
        """Goes back to where the last session stopped; ignores a row or filter that no longer exists."""
        row_filter = next((f for f in RowFilter if f.name == filter_name), RowFilter.ALL)
        self.set_filter(row_filter)
        row = next((r for r in self.rows if r.excel_row == excel_row), None)
        if row is not None:
            self.go_to(row)

    def next(self) -> bool:
        return self._move_to(next((i for i in self._shown() if self._after(i)), None))

    def previous(self) -> bool:
        return self._move_to(next((i for i in reversed(self._shown()) if self._before(i)), None))

    def next_unreviewed(self) -> bool:
        """The next row without a verdict that matches the filter, wrapping round to the start."""
        candidates = [i for i in self._shown() if self.rows[i].my_verdict is None]
        later = [i for i in candidates if self._after(i)]
        return self._move_to((later or candidates or [None])[0])

    def record(self, verdict: Optional[str]) -> bool:
        """Records the imager's verdict for the current row (None clears it), saves, and moves on when a
        verdict was given. Returns False when the save failed; the reason is in save_error."""
        row = self.current
        if row is None:
            return True
        if verdict is not None and verdict not in MY_VERDICTS:
            raise ValueError(f"Verdict must be one of {', '.join(MY_VERDICTS)}.")
        row.my_verdict = verdict
        self.workbook.set_verdict(row)
        saved = self.save()
        if verdict is not None and not self.next():
            # Last row in the filter: stay, unless it no longer matches (e.g. "not reviewed").
            if not self.filter.matches(row) and not self.previous():
                self._index = None
        return saved

    def record_many(self, rows: List[ReviewRow], verdict: Optional[str]) -> bool:
        """Records the verdict (None clears it) for every row, with one save. Stays on the current row,
        or moves to a neighbour when it no longer matches the filter."""
        if verdict is not None and verdict not in MY_VERDICTS:
            raise ValueError(f"Verdict must be one of {', '.join(MY_VERDICTS)}.")
        for row in rows:
            row.my_verdict = verdict
            self.workbook.set_verdict(row)
        saved = self.save()
        current = self.current
        if current is not None and not self.filter.matches(current) and not self.next() and not self.previous():
            self._index = None
        return saved

    def save(self) -> bool:
        try:
            self.workbook.save()
            self.save_error = None
            return True
        except WorkbookOpenError as e:
            self.save_error = str(e)
            return False

    @property
    def has_unsaved(self) -> bool:
        return bool(getattr(self.workbook, "unsaved", False))

    def set_filter(self, row_filter: RowFilter) -> None:
        """Keeps the current row if it matches, otherwise moves to the next one that does (or the first)."""
        self.filter = row_filter
        shown = self._shown()
        if self._index in shown:
            return
        later = [i for i in shown if self._index is None or i > self._index]
        self._index = (later or shown or [None])[0]

    def tally(self) -> Tally:
        return Tally.of(self.rows)

    def _shown(self) -> List[int]:
        return [i for i, r in enumerate(self.rows) if self.filter.matches(r)]

    def _after(self, i: int) -> bool:
        return self._index is None or i > self._index

    def _before(self, i: int) -> bool:
        return self._index is None or i < self._index

    def _move_to(self, i: Optional[int]) -> bool:
        if i is None:
            return False
        self._index = i
        return True

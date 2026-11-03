"""The table of the rows that match the filter; picking a row makes it the one under review."""
from __future__ import annotations

import tkinter as tk
from tkinter import ttk
from typing import Callable, Dict, List, Optional

from .review_row import ReviewRow
from .traceability import requirement

COLUMNS = (
    ("row", "Row", 50, "e"), ("night", "Night", 90, "w"), ("time", "Local time", 140, "w"),
    ("target", "Target", 110, "w"), ("monitor", "Monitor", 70, "w"), ("mine", "Mine", 70, "w"),
    ("agrees", "Agrees", 60, "w"), ("stars", "Stars", 80, "e"), ("fraction", "Star fraction", 90, "e"),
    ("brightness", "Brightness", 80, "e"), ("uniformity", "Uniformity", 80, "e"), ("why", "Picked because", 300, "w"),
)


def _num(value: Optional[float], digits: int = 2) -> str:
    return "" if value is None else f"{value:.{digits}f}"


def _values(row: ReviewRow) -> tuple:
    stars = "" if row.stars_found is None else f"{row.stars_found}/{'' if row.stars_expected is None else row.stars_expected}"
    agrees = "" if row.agrees is None else ("Yes" if row.agrees else "No")
    return (row.excel_row, row.night, row.local_time, row.target, row.verdict, row.my_verdict or "", agrees,
            stars, _num(row.star_fraction), _num(row.brightness), _num(row.uniformity), row.picked_because)


@requirement("REQ-62")
class ReviewTable(ttk.Frame):
    def __init__(self, master, on_pick: Callable[[ReviewRow], None], height: int = 8) -> None:
        super().__init__(master)
        self._on_pick = on_pick
        self._rows: Dict[str, ReviewRow] = {}
        self._order: List[str] = []
        self._quiet = False
        self.tree = ttk.Treeview(self, columns=[c[0] for c in COLUMNS], show="headings", height=height, selectmode="browse")
        for key, title, width, anchor in COLUMNS:
            self.tree.heading(key, text=title)
            self.tree.column(key, width=width, anchor=anchor, stretch=key == "why")
        self.tree.tag_configure("disagree", foreground="#B3261E")
        scroll = ttk.Scrollbar(self, orient="vertical", command=self.tree.yview)
        self.tree.configure(yscrollcommand=scroll.set)
        scroll.pack(side="right", fill="y")
        self.tree.pack(side="left", fill="both", expand=True)
        self.tree.bind("<<TreeviewSelect>>", self._picked)

    def show(self, rows: List[ReviewRow], current: Optional[ReviewRow]) -> None:
        """Lists rows (rebuilding only when the set changed), refreshes their values and selects current."""
        self._quiet = True
        try:
            order = [str(r.excel_row) for r in rows]
            if order != self._order:
                self.tree.delete(*self.tree.get_children())
                for iid in order:
                    self.tree.insert("", "end", iid=iid)
                self._order = order
            self._rows = {str(r.excel_row): r for r in rows}
            for iid, row in self._rows.items():
                values = _values(row)
                if tuple(str(v) for v in values) != tuple(self.tree.item(iid, "values")):
                    self.tree.item(iid, values=values)
                self.tree.item(iid, tags=("disagree",) if row.agrees is False else ())
            if current is not None and str(current.excel_row) in self._rows:
                iid = str(current.excel_row)
                self.tree.selection_set(iid)
                self.tree.see(iid)
            else:
                self.tree.selection_set(())
        finally:
            self._quiet = False

    def _picked(self, _event) -> None:
        selected = self.tree.selection()
        if not self._quiet and selected:
            self._on_pick(self._rows[selected[0]])

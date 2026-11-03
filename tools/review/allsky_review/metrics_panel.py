"""The panel beside the frame: the row's metrics, the verdicts, the counts and the keys."""
from __future__ import annotations

import tkinter as tk
from typing import Callable, Optional, Tuple

from .review_row import ReviewRow
from .row_filter import RowFilter
from .tally import Tally
from .traceability import requirement

VERDICT_COLOURS = {"Clear": "#1E7B34", "Cloud": "#B3261E", "Unknown": "#8A6200", "Unsure": "#555555"}
KEYS = (
    ("C", "Clear"), ("X", "Cloud"), ("U", "Unsure"), ("Delete", "clear my verdict"),
    ("← →", "previous / next"), ("N", "next not reviewed"), ("F", "change filter"), ("Click", "zoom"),
)


@requirement("REQ-62")
class MetricsPanel(tk.Frame):
    def __init__(self, master, on_filter: Callable[[RowFilter], None], **kw) -> None:
        super().__init__(master, padx=14, pady=12, **kw)
        self._on_filter = on_filter
        self._filter = tk.StringVar(value=RowFilter.ALL.value)
        self._values = {}
        self.columnconfigure(1, weight=1)
        r = 0
        tk.Label(self, text="Show", anchor="w").grid(row=r, column=0, sticky="w")
        menu = tk.OptionMenu(self, self._filter, *[f.value for f in RowFilter], command=self._filter_chosen)
        menu.configure(width=20)
        menu.grid(row=r, column=1, sticky="ew")
        r += 1
        self._position = tk.Label(self, anchor="w", font=("Helvetica", 12, "bold"))
        self._position.grid(row=r, column=0, columnspan=2, sticky="w", pady=(8, 8))
        r += 1
        for key, label in (("night", "Night"), ("time", "Local time"), ("target", "Target"), ("verdict", "Monitor"),
                           ("mine", "My verdict"), ("stars", "Stars found"), ("fraction", "Star fraction"),
                           ("brightness", "Brightness"), ("uniformity", "Non-uniformity"), ("why", "Picked because")):
            tk.Label(self, text=label, anchor="w", fg="#555555").grid(row=r, column=0, sticky="nw", pady=1)
            value = tk.Label(self, anchor="w", justify="left", wraplength=230)
            value.grid(row=r, column=1, sticky="w", pady=1)
            self._values[key] = value
            r += 1
        self._values["verdict"].configure(font=("Helvetica", 14, "bold"))
        self._values["mine"].configure(font=("Helvetica", 14, "bold"))
        tk.Frame(self, height=1, bg="#CCCCCC").grid(row=r, column=0, columnspan=2, sticky="ew", pady=10)
        r += 1
        self._counts = tk.Label(self, anchor="w", justify="left")
        self._counts.grid(row=r, column=0, columnspan=2, sticky="w")
        r += 1
        self._status = tk.Label(self, anchor="w", justify="left", wraplength=320, fg="#B3261E")
        self._status.grid(row=r, column=0, columnspan=2, sticky="w", pady=(8, 0))
        r += 1
        self.rowconfigure(r, weight=1)
        r += 1
        keys = tk.Frame(self)
        keys.grid(row=r, column=0, columnspan=2, sticky="sw")
        for i, (k, what) in enumerate(KEYS):
            tk.Label(keys, text=k, font=("Helvetica", 11, "bold"), anchor="w").grid(row=i, column=0, sticky="w")
            tk.Label(keys, text=what, anchor="w", fg="#555555").grid(row=i, column=1, sticky="w", padx=(8, 0))

    def show(self, row: Optional[ReviewRow], position: Tuple[int, int], tally: Tally, row_filter: RowFilter) -> None:
        self._filter.set(row_filter.value)
        n, m = position
        self._position.configure(text=f"{n} of {m}" if row is not None else ("All reviewed" if m == 0 else "—"))
        if row is None:
            for v in self._values.values():
                v.configure(text="")
        else:
            self._values["night"].configure(text=row.night)
            self._values["time"].configure(text=row.local_time)
            self._values["target"].configure(text=row.target)
            self._values["verdict"].configure(text=row.verdict, fg=VERDICT_COLOURS.get(row.verdict, "black"))
            mine = row.my_verdict or "—"
            agree = "" if row.agrees is None else ("  (agree)" if row.agrees else "  (disagree)")
            self._values["mine"].configure(text=mine + agree, fg=VERDICT_COLOURS.get(row.my_verdict or "", "black"))
            self._values["stars"].configure(text=_stars(row))
            self._values["fraction"].configure(text=_num(row.star_fraction, "{:.0%}"))
            self._values["brightness"].configure(text=_num(row.brightness, "{:.2f}×"))
            self._values["uniformity"].configure(text=_num(row.uniformity, "{:.2f}×"))
            self._values["why"].configure(text=row.picked_because)
        agreement = "—" if tally.agreement is None else f"{tally.agreement:.0%}"
        self._counts.configure(text=(
            f"Reviewed {tally.reviewed} of {tally.total}\n"
            f"Agree {tally.agree} · Disagree {tally.disagree} · Agreement {agreement}\n"
            f"False cloud (monitor Cloud, you Clear) {tally.false_cloud}\n"
            f"Missed cloud (monitor Clear, you Cloud) {tally.missed_cloud}"))

    def status(self, message: Optional[str]) -> None:
        self._status.configure(text=message or "")

    def _filter_chosen(self, text: str) -> None:
        self._on_filter(next(f for f in RowFilter if f.value == text))


def _num(value, fmt: str) -> str:
    return "—" if value is None else fmt.format(value)


def _stars(row: ReviewRow) -> str:
    if row.stars_expected is None:
        return "—"
    return f"{row.stars_found if row.stars_found is not None else '—'} of {row.stars_expected}"

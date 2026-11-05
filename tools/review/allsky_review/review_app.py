"""The review window: the frame, the metrics panel and the keys."""
from __future__ import annotations

import json
import sys
import tkinter as tk
from pathlib import Path
from tkinter import filedialog, messagebox
from typing import Optional

from . import __version__
from .frame_view import FrameView
from .metrics_panel import MetricsPanel
from .review_session import ReviewSession
from .review_table import ReviewTable
from .review_workbook import ReviewWorkbook
from .row_filter import RowFilter
from .traceability import requirement

STATE_FILE = ".review-state.json"


@requirement("REQ-62")
class ReviewApp:
    def __init__(self, root: tk.Tk, session: ReviewSession) -> None:
        self.root = root
        self.session = session
        root.title(f"Allsky review — {session.workbook.run_folder}")
        width = min(1400, root.winfo_screenwidth())
        height = min(900, root.winfo_screenheight())
        x = (root.winfo_screenwidth() - width) // 2
        y = (root.winfo_screenheight() - height) // 2
        root.geometry(f"{width}x{height}+{x}+{y}")
        root.minsize(900, 600)
        header = tk.Frame(root)
        header.pack(side="top", fill="x", padx=10, pady=(4, 0))
        tk.Label(header, text=f"Allsky review — {session.workbook.run_folder}", anchor="w").pack(side="left")
        tk.Label(header, text=f"v{__version__}", font=("Helvetica", 9), fg="#777777").pack(side="right")
        self.table = ReviewTable(root, on_pick=self._pick)
        self.table.pack(side="bottom", fill="x")
        self.frame = FrameView(root)
        self.frame.pack(side="left", fill="both", expand=True)
        self.metrics = MetricsPanel(root, on_filter=self._set_filter, width=360)
        self.metrics.pack(side="right", fill="y")
        for key, verdict in (("c", "Clear"), ("x", "Cloud"), ("u", "Unsure")):
            root.bind(f"<KeyPress-{key}>", lambda _e, v=verdict: self._record(v))
            root.bind(f"<KeyPress-{key.upper()}>", lambda _e, v=verdict: self._record(v))
        for key in ("<Delete>", "<BackSpace>"):
            root.bind(key, lambda _e: self._record(None))
        root.bind("<Down>", lambda _e: self._wheel_step(1))
        root.bind("<Up>", lambda _e: self._wheel_step(-1))
        self.table.tree.bind("<Down>", lambda _e: self._wheel_step(1))
        self.table.tree.bind("<Up>", lambda _e: self._wheel_step(-1))
        for key in ("n", "N"):
            root.bind(f"<KeyPress-{key}>", lambda _e: self._move(self.session.next_unreviewed, "No frames left to review in this filter"))
        for key in ("f", "F"):
            root.bind(f"<KeyPress-{key}>", lambda _e: self._set_filter(self.session.filter.following()))
        root.protocol("WM_DELETE_WINDOW", self._close)
        for widget in (root, self.table.tree):
            widget.bind("<MouseWheel>", self._wheel)
            widget.bind("<Button-4>", lambda _e: self._wheel_step(-1))
            widget.bind("<Button-5>", lambda _e: self._wheel_step(1))
        self._restore_state()
        self._shown_row = None
        self._refresh()

    def _wheel(self, event) -> str:
        self._wheel_step(-1 if event.delta > 0 else 1)
        return "break"

    def _wheel_step(self, direction: int) -> str:
        if direction < 0:
            self._move(self.session.previous, "First frame in this filter")
        else:
            self._move(self.session.next, "Last frame in this filter")
        return "break"

    def _state_path(self) -> Path:
        return self.session.workbook.run_folder / STATE_FILE

    def _restore_state(self) -> None:
        try:
            state = json.loads(self._state_path().read_text(encoding="utf-8"))
            self.session.restore(state.get("row"), state.get("filter"))
        except (OSError, ValueError, AttributeError):
            pass

    def _save_state(self) -> None:
        row = self.session.current
        state = {"row": row.excel_row if row else None, "filter": self.session.filter.name}
        try:
            self._state_path().write_text(json.dumps(state), encoding="utf-8")
        except OSError:
            pass

    def _record(self, verdict: Optional[str]) -> None:
        if self.session.current is None:
            return
        picked = self.table.selected_rows()
        if len(picked) > 1:
            self.session.record_many(picked, verdict)
        else:
            self.session.record(verdict)
        self._refresh(self.session.save_error)

    def _move(self, step, at_end: str) -> None:
        self._refresh(None if step() else at_end)

    def _set_filter(self, row_filter: RowFilter) -> None:
        self.session.set_filter(row_filter)
        self._refresh()

    def _pick(self, row) -> None:
        if row is not self.session.current:
            self.session.go_to(row)
            self._refresh()

    def _refresh(self, message: Optional[str] = None) -> None:
        row = self.session.current
        self.table.show(self.session.shown_rows(), row)
        if row is not self._shown_row:
            self.frame.show(row.image if row is not None else None)
            self._shown_row = row
        self.metrics.show(row, self.session.position(), self.session.tally(), self.session.filter)
        self.metrics.status(message or self.session.save_error)

    def _close(self) -> None:
        while self.session.has_unsaved and not self.session.save():
            if not messagebox.askretrycancel(
                    "Verdicts not saved",
                    f"{self.session.save_error}\n\nRetry after closing it in Excel, or Cancel to quit without saving the last verdicts."):
                break
        self._save_state()
        self.root.destroy()


def main(argv=None) -> int:
    argv = sys.argv[1:] if argv is None else argv
    root = tk.Tk()
    folder = argv[0] if argv else filedialog.askdirectory(title="Choose a replay run folder (it holds review.xlsx)")
    if not folder:
        root.destroy()
        return 1
    try:
        session = ReviewSession(ReviewWorkbook(Path(folder)))
    except (OSError, ValueError) as e:
        messagebox.showerror("Allsky review", str(e))
        root.destroy()
        return 1
    if not session.rows:
        messagebox.showinfo("Allsky review", "The Review sheet has no frames to review.")
        root.destroy()
        return 0
    ReviewApp(root, session)
    root.mainloop()
    return 0

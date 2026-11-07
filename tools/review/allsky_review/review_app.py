"""The review window: the frame, the metrics panel and the keys."""
from __future__ import annotations

import json
import sys
import tkinter as tk
from pathlib import Path
from tkinter import filedialog, messagebox
from typing import Optional

from PIL import Image

from . import __version__
from .frame_view import FrameView
from .metrics_panel import MetricsPanel
from .review_session import ReviewSession
from .review_table import ReviewTable
from .review_workbook import ReviewWorkbook
from .row_filter import RowFilter
from .settings_window import SettingsWindow
from .traceability import requirement
from .working_circle import SettingsFile, WorkingArea, draw_working_area

STATE_FILE = ".review-state.json"


@requirement("REQ-62", "REQ-63")
class ReviewApp:
    def __init__(self, root: tk.Tk, session: ReviewSession) -> None:
        self.root = root
        self.session = session
        self._circle_area: Optional[WorkingArea] = None
        self._circle_message: Optional[str] = None
        self._circle_window: Optional[SettingsWindow] = None
        self._circle_settings: Optional[SettingsFile] = None
        self._circle_run_area: Optional[WorkingArea] = None
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
        for key in ("s", "S"):
            root.bind(f"<KeyPress-{key}>", lambda _e: self._open_circle_editor())
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

    def _open_circle_editor(self) -> None:
        if self._circle_window is not None and self._circle_window.winfo_exists():
            self._circle_window.lift()
            return
        try:
            path = self.session.workbook.settings_path()
            if path is None or not path.is_file():
                raise ValueError("This replay has no accessible settings file. Run the harness again with the settings file available.")
            settings = SettingsFile(path)
            run_area = self.session.workbook.run_working_area()
            if run_area is not None:
                run_area.validate()
            self._circle_settings = settings
            self._circle_run_area = run_area
            self._circle_window = SettingsWindow(
                self.root, settings, run_area, self._preview_working_area
            )
        except (OSError, ValueError) as e:
            messagebox.showerror("Working circle", str(e), parent=self.root)

    def _preview_working_area(self, area: Optional[WorkingArea]) -> None:
        self._circle_area = area
        self._circle_message = None
        row = self.session.current
        if area is None:
            if row is not None:
                self.frame.show(row.image)
                self._shown_row = row
        elif row is not None:
            image_path = row.original_image
            if image_path is None:
                image_path = row.image
                if image_path is not None:
                    self._circle_message = "Original frame path is not recorded; using the marked-up review image."
            elif not image_path.is_file():
                image_path = row.image
                if image_path is not None:
                    self._circle_message = "Original frame is unavailable; using the marked-up review image."
            if image_path is None:
                self._circle_message = "No saved image is available for this frame."
                self.frame.show(None)
            elif self._circle_settings is None:
                self._circle_message = "The working-circle settings are unavailable."
                self.frame.show(None)
            else:
                try:
                    with Image.open(image_path) as image:
                        preview = draw_working_area(image, self._circle_settings.lens, area, self._circle_run_area)
                    self.frame.show_image(preview)
                except (OSError, ValueError) as e:
                    self._circle_message = f"Can't render preview for {image_path.name}: {getattr(e, 'strerror', None) or e}"
                    self.frame.show(None)
            self._shown_row = row
        self.metrics.status(self._circle_message)

    def _refresh(self, message: Optional[str] = None) -> None:
        row = self.session.current
        self.table.show(self.session.shown_rows(), row)
        if self._circle_area is not None:
            self._preview_working_area(self._circle_area)
        elif row is not self._shown_row:
            self.frame.show(row.image if row is not None else None)
            self._shown_row = row
        self.metrics.show(row, self.session.position(), self.session.tally(), self.session.filter)
        self.metrics.status(message or self._circle_message or self.session.save_error)

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

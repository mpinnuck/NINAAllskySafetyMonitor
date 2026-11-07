"""Editor for the working circle in the settings file used by a replay."""
from __future__ import annotations

import tkinter as tk
from tkinter import messagebox
from typing import Callable, Optional

from .traceability import requirement
from .working_circle import SettingsFile, WorkingArea


@requirement("REQ-63")
class SettingsWindow(tk.Toplevel):
    def __init__(
        self,
        master: tk.Tk,
        settings: SettingsFile,
        run_area: Optional[WorkingArea],
        on_preview: Callable[[Optional[WorkingArea]], None],
    ) -> None:
        super().__init__(master)
        self.settings = settings
        self.on_preview = on_preview
        self.title("Working circle")
        self.resizable(False, False)
        self.transient(master)
        self.protocol("WM_DELETE_WINDOW", self.close)
        self._vars = {
            "alt": tk.StringVar(value=f"{settings.area.centre_alt:g}"),
            "az": tk.StringVar(value=f"{settings.area.centre_az:g}"),
            "radius": tk.StringVar(value=f"{settings.area.radius:g}"),
        }
        tk.Label(self, text="Working circle used for this replay:", anchor="w").grid(
            row=0, column=0, columnspan=2, sticky="w", padx=12, pady=(12, 2)
        )
        run_text = (
            f"alt {run_area.centre_alt:g}° · az {run_area.centre_az:g}° · radius {run_area.radius:g}°"
            if run_area is not None else "Not recorded in this workbook"
        )
        tk.Label(self, text=run_text,
                 anchor="w").grid(row=1, column=0, columnspan=2, sticky="w", padx=12, pady=(0, 10))
        tk.Label(
            self,
            text="Arrow keys: move centre · Shift + arrow: 5°",
            anchor="w",
            fg="#555555",
        ).grid(row=2, column=0, columnspan=2, sticky="w", padx=12, pady=(0, 4))
        for row, (key, label) in enumerate((
            ("alt", "Centre altitude (0–90°)"),
            ("az", "Centre azimuth (0–360°)"),
            ("radius", "Radius (5–90°)"),
        ), start=3):
            tk.Label(self, text=label, anchor="w").grid(row=row, column=0, sticky="w", padx=12, pady=4)
            entry = tk.Entry(self, textvariable=self._vars[key], width=16)
            entry.grid(row=row, column=1, sticky="e", padx=12, pady=4)
            entry.bind("<Return>", lambda _event: self.apply())
        for direction in ("Up", "Down", "Left", "Right"):
            self.bind(f"<{direction}>", lambda event, d=direction: self._nudge(d, event))
        self._status = tk.StringVar()
        tk.Label(self, textvariable=self._status, anchor="w", fg="#555555", wraplength=380).grid(
            row=6, column=0, columnspan=2, sticky="w", padx=12, pady=(4, 8)
        )
        buttons = tk.Frame(self)
        buttons.grid(row=7, column=0, columnspan=2, sticky="e", padx=12, pady=(0, 12))
        tk.Button(buttons, text="Apply", command=self.apply).pack(side="left", padx=3)
        tk.Button(buttons, text="Save", command=self.save).pack(side="left", padx=3)
        tk.Button(buttons, text="Revert", command=self.revert).pack(side="left", padx=3)
        tk.Button(buttons, text="Close", command=self.close).pack(side="left", padx=3)
        self.on_preview(settings.area)

    def _nudge(self, direction: str, event) -> str:
        try:
            area = self._read_area().nudged(direction, 5 if event.state & 0x0001 else 1)
            area.validate()
        except ValueError as e:
            messagebox.showerror("Working circle", str(e), parent=self)
            return "break"
        self._vars["alt"].set(f"{area.centre_alt:g}")
        self._vars["az"].set(f"{area.centre_az:g}")
        self.apply()
        return "break"

    def _read_area(self) -> WorkingArea:
        try:
            area = WorkingArea(
                float(self._vars["alt"].get()),
                float(self._vars["az"].get()),
                float(self._vars["radius"].get()),
                self.settings.area.outline,
            )
        except ValueError as e:
            raise ValueError("Enter valid numbers for altitude, azimuth and radius.") from e
        area.validate()
        return area

    def apply(self) -> bool:
        try:
            area = self._read_area()
        except ValueError as e:
            messagebox.showerror("Working circle", str(e), parent=self)
            return False
        self.on_preview(area)
        self._status.set("Preview updated. Save writes only the circle values to settings.json.")
        return True

    def save(self) -> None:
        if not self.apply():
            return
        try:
            self.settings.save_working_area(self._read_area())
        except (OSError, ValueError) as e:
            messagebox.showerror("Working circle", f"Could not save settings:\n{e}", parent=self)
            return
        self._status.set(f"Saved to {self.settings.path}. Backup: {self.settings.path.name}.bak")

    def revert(self) -> None:
        try:
            self.settings.reload()
        except (OSError, ValueError) as e:
            messagebox.showerror("Working circle", f"Could not reload settings:\n{e}", parent=self)
            return
        self._vars["alt"].set(f"{self.settings.area.centre_alt:g}")
        self._vars["az"].set(f"{self.settings.area.centre_az:g}")
        self._vars["radius"].set(f"{self.settings.area.radius:g}")
        self.on_preview(self.settings.area)
        self._status.set("Reverted to the values currently saved in settings.json.")

    def close(self) -> None:
        self.on_preview(None)
        self.destroy()

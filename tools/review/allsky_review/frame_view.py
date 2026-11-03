"""Shows a marked-up frame, fitted to the window, with click-to-zoom."""
from __future__ import annotations

import tkinter as tk
from pathlib import Path
from typing import Optional, Tuple

from PIL import Image, ImageTk

from .traceability import requirement

ZOOM = 3.0


@requirement("REQ-62")
class FrameView(tk.Canvas):
    """A click zooms to 3× of the fitted size, centred on the point clicked; another click returns to fit."""

    def __init__(self, master, **kw) -> None:
        super().__init__(master, background="#111111", highlightthickness=0, **kw)
        self._image: Optional[Image.Image] = None
        self._photo: Optional[ImageTk.PhotoImage] = None
        self._centre: Optional[Tuple[float, float]] = None  # image coordinates when zoomed
        self._message = ""
        self.bind("<Configure>", lambda _e: self._render())
        self.bind("<Button-1>", lambda e: self.zoom_at(e.x, e.y))

    def show(self, image: Optional[Path]) -> None:
        self._centre = None
        self._image = None
        self._message = ""
        if image is None:
            self._message = "No image for this row"
        else:
            try:
                with Image.open(image) as im:
                    self._image = im.convert("RGB")
            except OSError as e:
                self._message = f"Can't open {image.name}: {e.strerror or e}"
        self._render()

    def zoom_at(self, x: int, y: int) -> None:
        if self._image is None:
            return
        if self._centre is not None:
            self._centre = None
        else:
            scale, ox, oy = self._fit()
            self._centre = ((x - ox) / scale, (y - oy) / scale)
        self._render()

    def _fit(self) -> Tuple[float, float, float]:
        w, h = max(self.winfo_width(), 1), max(self.winfo_height(), 1)
        iw, ih = self._image.size
        scale = min(w / iw, h / ih)
        return scale, (w - iw * scale) / 2, (h - ih * scale) / 2

    def _render(self) -> None:
        self.delete("all")
        w, h = max(self.winfo_width(), 1), max(self.winfo_height(), 1)
        if self._image is None:
            self.create_text(w / 2, h / 2, text=self._message, fill="#DDDDDD", font=("Helvetica", 14))
            return
        scale, ox, oy = self._fit()
        if self._centre is None:
            shown = self._image.resize((max(1, int(self._image.width * scale)), max(1, int(self._image.height * scale))), Image.LANCZOS)
            x, y = ox, oy
        else:
            z = scale * ZOOM
            cx, cy = self._centre
            # The part of the image that fills the canvas at this zoom, kept inside the image.
            half_w, half_h = w / z / 2, h / z / 2
            left = min(max(cx - half_w, 0), max(self._image.width - 2 * half_w, 0))
            top = min(max(cy - half_h, 0), max(self._image.height - 2 * half_h, 0))
            box = (int(left), int(top), int(min(left + 2 * half_w, self._image.width)), int(min(top + 2 * half_h, self._image.height)))
            crop = self._image.crop(box)
            shown = crop.resize((max(1, int(crop.width * z)), max(1, int(crop.height * z))), Image.LANCZOS)
            x, y = (w - shown.width) / 2, (h - shown.height) / 2
        self._photo = ImageTk.PhotoImage(shown)
        self.create_image(x, y, image=self._photo, anchor="nw")
        hint = "click to fit" if self._centre is not None else "click to zoom"
        self.create_text(w - 8, h - 8, text=hint, fill="#AAAAAA", anchor="se", font=("Helvetica", 11))

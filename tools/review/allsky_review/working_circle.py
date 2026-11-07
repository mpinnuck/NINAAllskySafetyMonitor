"""Working-area settings, projection and preview drawing for REQ-63."""
from __future__ import annotations

import json
import math
import os
import shutil
import tempfile
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Optional, Tuple

from PIL import Image, ImageDraw, ImageFont

from .traceability import requirement


@requirement("REQ-63")
@dataclass(frozen=True)
class WorkingArea:
    centre_alt: float
    centre_az: float
    radius: float
    outline: tuple[Tuple[float, float], ...] = ()

    def nudged(self, direction: str, step: float = 1) -> WorkingArea:
        if direction == "Up":
            return WorkingArea(min(90, self.centre_alt + step), self.centre_az, self.radius, self.outline)
        if direction == "Down":
            return WorkingArea(max(0, self.centre_alt - step), self.centre_az, self.radius, self.outline)
        if direction == "Right":
            return WorkingArea(self.centre_alt, (self.centre_az + step) % 360, self.radius, self.outline)
        if direction == "Left":
            return WorkingArea(self.centre_alt, (self.centre_az - step) % 360, self.radius, self.outline)
        raise ValueError(f"Unknown working-circle direction: {direction}.")

    def validate(self) -> None:
        if not math.isfinite(self.centre_alt) or not 0 <= self.centre_alt <= 90:
            raise ValueError("Centre altitude must be between 0° and 90°.")
        if not math.isfinite(self.centre_az) or not 0 <= self.centre_az <= 360:
            raise ValueError("Centre azimuth must be between 0° and 360°.")
        if not math.isfinite(self.radius) or not 5 <= self.radius <= 90:
            raise ValueError("Radius must be between 5° and 90°.")


@requirement("REQ-63")
class LensModel:
    def __init__(self, value: dict[str, Any]) -> None:
        self.centre_x = _number(_get(value, "CentreX"), "lens centre X")
        self.centre_y = _number(_get(value, "CentreY"), "lens centre Y")
        self.north_rotation = _number(_get(value, "NorthRotationDeg"), "lens north rotation")
        self.east_clockwise = bool(_get(value, "EastIsClockwise"))
        coefficients = _get(value, "RadialCoefficients")
        if not isinstance(coefficients, list) or not coefficients:
            raise ValueError("The settings file has no usable lens radial coefficients.")
        self.coefficients = [_number(c, "lens radial coefficient") for c in coefficients]
        if self.coefficients[0] <= 0:
            raise ValueError("The settings file has no usable lens radial coefficients.")
        width = _number(_get(value, "ImageWidth") or 0, "lens image width")
        height = _number(_get(value, "ImageHeight") or 0, "lens image height")
        if width < 0 or height < 0 or not width.is_integer() or not height.is_integer():
            raise ValueError("The settings file has invalid lens image dimensions.")
        self.image_width = int(width)
        self.image_height = int(height)

    def to_pixel(self, alt: float, az: float) -> Tuple[float, float]:
        z = math.radians(90 - alt)
        radius = sum(c * z ** (i + 1) for i, c in enumerate(self.coefficients))
        theta = math.radians(self.north_rotation + (az if self.east_clockwise else -az))
        return self.centre_x + radius * math.sin(theta), self.centre_y - radius * math.cos(theta)


@requirement("REQ-63")
class SettingsFile:
    def __init__(self, path: Path) -> None:
        self.path = Path(path).expanduser().resolve()
        self.reload()

    def reload(self) -> None:
        try:
            value = json.loads(self.path.read_text(encoding="utf-8"))
        except json.JSONDecodeError as e:
            raise ValueError(f"{self.path} is not valid settings JSON: {e}") from e
        if not isinstance(value, dict):
            raise ValueError(f"{self.path} does not contain a settings object.")
        self.value = value
        calibration = _get(value, "Calibration")
        if not isinstance(calibration, dict):
            raise ValueError(f"{self.path} has no Calibration settings.")
        area = _get(calibration, "WorkingArea")
        if not isinstance(area, dict):
            raise ValueError(f"{self.path} has no working-area settings.")
        outline = _get(area, "Outline") or []
        if not isinstance(outline, list):
            raise ValueError(f"{self.path} has an invalid working-area outline.")
        outline_points = []
        for point in outline:
            if not isinstance(point, dict):
                raise ValueError(f"{self.path} has an invalid working-area outline.")
            outline_points.append((_number(_get(point, "X"), "outline X"), _number(_get(point, "Y"), "outline Y")))
        self.area = WorkingArea(
            _number(_get(area, "CentreAltDeg"), "centre altitude"),
            _number(_get(area, "CentreAzDeg"), "centre azimuth"),
            _number(_get(area, "RadiusDeg"), "radius"),
            tuple(outline_points),
        )
        self.area.validate()
        lens = _get(calibration, "Lens")
        if not isinstance(lens, dict):
            raise ValueError(f"{self.path} has no lens calibration; the working circle cannot be drawn.")
        self.lens = LensModel(lens)

    def save_working_area(self, area: WorkingArea) -> None:
        area.validate()
        calibration = _get(self.value, "Calibration")
        settings_area = _get(calibration, "WorkingArea")
        _set(settings_area, "CentreAltDeg", area.centre_alt)
        _set(settings_area, "CentreAzDeg", area.centre_az)
        _set(settings_area, "RadiusDeg", area.radius)
        backup = self.path.with_name(self.path.name + ".bak")
        temp_path: Optional[Path] = None
        try:
            if not backup.exists():
                shutil.copy2(self.path, backup)
            with tempfile.NamedTemporaryFile(
                mode="w", encoding="utf-8", dir=self.path.parent,
                prefix=self.path.name + ".", suffix=".tmp", delete=False,
            ) as temp:
                temp_path = Path(temp.name)
                json.dump(self.value, temp, indent=2, ensure_ascii=False)
                temp.write("\n")
                temp.flush()
                os.fsync(temp.fileno())
            os.replace(temp_path, self.path)
        finally:
            if temp_path is not None and temp_path.exists():
                temp_path.unlink()
        self.reload()


@requirement("REQ-63")
def draw_working_area(
    frame: Image.Image,
    lens: LensModel,
    area: WorkingArea,
    run_area: Optional[WorkingArea] = None,
) -> Image.Image:
    area.validate()
    if run_area is not None:
        run_area.validate()
    if (lens.image_width and lens.image_width != frame.width) or (lens.image_height and lens.image_height != frame.height):
        raise ValueError(
            f"Lens calibration is for {lens.image_width}×{lens.image_height}, "
            f"but this frame is {frame.width}×{frame.height}."
        )
    result = frame.convert("RGB").copy()
    draw = ImageDraw.Draw(result)
    points = []
    for bearing in range(0, 361, 2):
        alt, az = _destination(area.centre_alt, area.centre_az, bearing, area.radius)
        points.append(lens.to_pixel(max(0, alt), az))
    for i in range(0, len(points) - 1, 2):
        draw.line((points[i], points[i + 1]), fill="#FFD54F", width=max(2, result.width // 400))
    if len(area.outline) >= 3:
        draw.line(area.outline + (area.outline[0],), fill="#4FC3F7", width=max(2, result.width // 400))
    preview_text = f"Preview: alt {area.centre_alt:g}° · az {area.centre_az:g}° · radius {area.radius:g}°"
    run_text = (
        f"Run used: alt {run_area.centre_alt:g}° · az {run_area.centre_az:g}° · radius {run_area.radius:g}°"
        if run_area is not None else "Run used: not recorded in this workbook"
    )
    draw.rectangle((0, 0, result.width, 42), fill=(0, 0, 0))
    font = ImageFont.load_default()
    draw.text((8, 4), preview_text, fill="white", font=font)
    draw.text((8, 20), run_text, fill="white", font=font)
    return result


def _destination(alt: float, az: float, bearing: float, distance: float) -> Tuple[float, float]:
    rad = math.pi / 180
    if alt > 90 - 1e-9:
        return 90 - distance, (az + 180 - bearing) % 360
    lat1, lon1, brg, d = alt * rad, az * rad, bearing * rad, distance * rad
    lat2 = math.asin(math.sin(lat1) * math.cos(d) + math.cos(lat1) * math.sin(d) * math.cos(brg))
    lon2 = lon1 + math.atan2(
        math.sin(brg) * math.sin(d) * math.cos(lat1),
        math.cos(d) - math.sin(lat1) * math.sin(lat2),
    )
    return lat2 / rad, (lon2 / rad) % 360


def _get(mapping: Any, name: str) -> Any:
    if not isinstance(mapping, dict):
        return None
    return next((value for key, value in mapping.items() if key.casefold() == name.casefold()), None)


def _set(mapping: dict[str, Any], name: str, value: Any) -> None:
    key = next((key for key in mapping if key.casefold() == name.casefold()), name)
    mapping[key] = value


def _number(value: Any, label: str) -> float:
    try:
        result = float(value)
    except (TypeError, ValueError) as e:
        raise ValueError(f"The settings file has an invalid {label}.") from e
    if not math.isfinite(result):
        raise ValueError(f"The settings file has an invalid {label}.")
    return result

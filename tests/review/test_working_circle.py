import json

import pytest
from PIL import Image

from allsky_review.traceability import requirement
from allsky_review.working_circle import LensModel, SettingsFile, WorkingArea, draw_working_area


def settings_data():
    return {
        "Cloud": {"MinStarFraction": 0.7},
        "Calibration": {
            "Lens": {
                "CentreX": 50,
                "CentreY": 50,
                "NorthRotationDeg": 0,
                "EastIsClockwise": False,
                "RadialCoefficients": [30 / (3.141592653589793 / 2)],
                "ImageWidth": 100,
                "ImageHeight": 100,
            },
            "WorkingArea": {
                "CentreAltDeg": 75,
                "CentreAzDeg": 180,
                "RadiusDeg": 60,
                "Outline": [{"X": 10, "Y": 20}, {"X": 80, "Y": 20}, {"X": 80, "Y": 90}],
            },
        },
    }


@requirement("REQ-63")
class TestWorkingCircle:
    @pytest.mark.parametrize(
        ("area", "message"),
        [
            (WorkingArea(-1, 180, 60), "altitude"),
            (WorkingArea(75, 361, 60), "azimuth"),
            (WorkingArea(75, 180, 4), "Radius"),
            (WorkingArea(float("nan"), 180, 60), "altitude"),
        ],
    )
    def test_working_area_validates_ranges(self, area, message):
        with pytest.raises(ValueError, match=message):
            area.validate()

    @pytest.mark.parametrize(
        ("direction", "step", "expected_alt", "expected_az"),
        [
            ("Up", 1, 76, 180),
            ("Down", 5, 70, 180),
            ("Left", 1, 75, 179),
            ("Right", 5, 75, 185),
        ],
    )
    def test_arrow_nudges_move_centre_without_changing_radius_or_outline(
        self, direction, step, expected_alt, expected_az
    ):
        area = WorkingArea(75, 180, 60, ((1, 2), (3, 4), (5, 6)))

        moved = area.nudged(direction, step)

        assert moved == WorkingArea(expected_alt, expected_az, 60, area.outline)

    def test_altitude_nudges_clamp_and_azimuth_nudges_wrap(self):
        area = WorkingArea(89, 359, 60)

        assert area.nudged("Up", 5).centre_alt == 90
        assert area.nudged("Right", 5).centre_az == 4
        assert WorkingArea(1, 1, 60).nudged("Down", 5).centre_alt == 0
        assert WorkingArea(1, 1, 60).nudged("Left", 5).centre_az == 356

    def test_settings_save_only_changes_working_area_and_keeps_first_backup(self, tmp_path):
        path = tmp_path / "settings.json"
        original = json.dumps(settings_data(), indent=2)
        path.write_text(original, encoding="utf-8")
        settings = SettingsFile(path)
        area = WorkingArea(65, 200, 45, settings.area.outline)

        settings.save_working_area(area)
        saved = json.loads(path.read_text(encoding="utf-8"))
        backup = path.with_name("settings.json.bak")
        assert json.loads(backup.read_text(encoding="utf-8")) == json.loads(original)
        assert saved["Cloud"] == {"MinStarFraction": 0.7}
        assert saved["Calibration"]["Lens"] == settings_data()["Calibration"]["Lens"]
        assert saved["Calibration"]["WorkingArea"] == {
            "CentreAltDeg": 65,
            "CentreAzDeg": 200,
            "RadiusDeg": 45,
            "Outline": settings_data()["Calibration"]["WorkingArea"]["Outline"],
        }
        assert settings.area == area
        assert list(tmp_path.glob("settings.json.*.tmp")) == []

        path.write_text(path.read_text(encoding="utf-8").replace('"CentreAltDeg": 65', '"CentreAltDeg": 66'), encoding="utf-8")
        settings.reload()
        settings.save_working_area(WorkingArea(67, 201, 46, settings.area.outline))
        assert json.loads(backup.read_text(encoding="utf-8")) == json.loads(original)

    def test_lens_projection_and_circle_preview_use_original_frame(self):
        lens = LensModel(settings_data()["Calibration"]["Lens"])
        area = WorkingArea(90, 0, 10)
        original = Image.new("RGB", (100, 100), "#111111")

        assert lens.to_pixel(90, 0) == (50, 50)
        preview = draw_working_area(original, lens, area)

        assert preview.size == original.size
        assert preview.tobytes() != original.tobytes()
        assert original.getpixel((50, 50)) == (17, 17, 17)

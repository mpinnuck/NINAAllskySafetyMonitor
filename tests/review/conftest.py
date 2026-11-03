"""Shared fixtures for the review tool tests: a run folder with a review.xlsx laid out like the harness writes it."""
import sys
from datetime import datetime
from pathlib import Path

import pytest
from openpyxl import Workbook
from openpyxl.worksheet.datavalidation import DataValidation

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools" / "review"))

HEADINGS = ["Night", "Local time", "Target", "Verdict", "Stars found", "Stars expected", "Star fraction",
            "Relative brightness", "Non-uniformity ratio", "Picked because", "Image", "My verdict", "Agrees"]

# night, time, target, verdict, found, expected, fraction, brightness, uniformity, why, my verdict
ROWS = [
    ("2026-10-20", "21:05:12", "RA01 Dec-45", "Cloud", 4, 13, 0.31, 1.04, 3.18, "Clear → Cloud", None),
    ("2026-10-20", "21:06:12", "RA02 Dec-35", "Clear", 9, 10, 0.90, None, None, "star fraction near limit", "Clear"),
    ("2026-10-20", "21:07:12", "RA03 Dec-25", "Cloud", 2, 12, 0.17, 1.40, 1.20, "Clear → Cloud", None),
    ("2026-10-20", "21:08:12", "RA04 Dec-15", "Clear", 12, 12, 1.0, 1.0, 1.0, "Cloud → Clear", "Cloud"),
    ("2026-10-20", "21:09:12", "RA05 Dec-05", "Unknown", 3, 5, None, None, None, "Clear → Unknown", None),
]


def write_review(folder: Path, rows=ROWS) -> Path:
    book = Workbook()
    sheet = book.active
    sheet.title = "Review"
    sheet.append(HEADINGS)
    for i, (night, time, target, verdict, found, expected, fraction, bright, uni, why, mine) in enumerate(rows, start=2):
        image = f"review/{night.replace('-', '')}/{time.replace(':', '')}-{target.replace(' ', '_').replace('-', 'm')}.jpg"
        sheet.append([datetime.strptime(night, "%Y-%m-%d"), datetime.strptime(f"{night} {time}", "%Y-%m-%d %H:%M:%S"),
                      target, verdict, found, expected, fraction, bright, uni, why,
                      f'=HYPERLINK("{image}","open")', mine,
                      f'=IF(L{i}="","",IF(L{i}="Unsure","",IF(L{i}=D{i},"Yes","No")))'])
    dv = DataValidation(type="list", formula1='"Clear,Cloud,Unsure"', allow_blank=True)
    sheet.add_data_validation(dv)
    dv.add(f"L2:L{len(rows) + 1}")
    summary = book.create_sheet("Summary")
    summary["A4"], summary["B4"] = "Agree", f'=COUNTIF(Review!$M$2:$M${len(rows) + 1},"Yes")'
    path = folder / "review.xlsx"
    book.save(path)
    return path


@pytest.fixture
def run_folder(tmp_path) -> Path:
    write_review(tmp_path)
    return tmp_path

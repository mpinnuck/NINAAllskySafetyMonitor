from openpyxl import load_workbook
import pytest

from allsky_review.review_workbook import ReviewWorkbook
from allsky_review.traceability import requirement
from allsky_review.workbook_open_error import WorkbookOpenError


@requirement("REQ-61", "REQ-62")
class TestReviewWorkbook:
    def test_reads_every_row_with_its_metrics_and_image(self, run_folder):
        rows = ReviewWorkbook(run_folder).load()

        assert [r.target for r in rows] == ["RA01 Dec-45", "RA02 Dec-35", "RA03 Dec-25", "RA04 Dec-15", "RA05 Dec-05"]
        first = rows[0]
        assert (first.excel_row, first.night, first.local_time, first.verdict) == (2, "2026-10-20", "2026-10-20 21:05:12", "Cloud")
        assert (first.stars_found, first.stars_expected, first.star_fraction) == (4, 13, 0.31)
        assert (first.brightness, first.uniformity, first.picked_because) == (1.04, 3.18, "Clear → Cloud")
        assert first.image == run_folder / "review/20261020/210512-RA01_Decm45.jpg"
        assert first.my_verdict is None
        assert rows[1].my_verdict == "Clear"
        assert rows[4].star_fraction is None

    def test_verdict_is_written_to_my_verdict_only_and_formulas_and_dropdown_survive(self, run_folder):
        wb = ReviewWorkbook(run_folder)
        row = wb.load()[0]
        row.my_verdict = "Cloud"
        wb.set_verdict(row)
        wb.save()

        book = load_workbook(run_folder / "review.xlsx")
        review = book["Review"]
        assert review["L2"].value == "Cloud"
        assert review["K2"].value.startswith("=HYPERLINK(")
        assert review["M2"].value.startswith("=IF(")
        assert review["L3"].value == "Clear"
        assert any("L2" in str(dv.sqref) for dv in review.data_validations.dataValidation)
        assert book["Summary"]["B4"].value.startswith("=COUNTIF(")
        assert not (run_folder / "review.xlsx.saving").exists()
        assert ReviewWorkbook(run_folder).load()[0].my_verdict == "Cloud"

    def test_clearing_a_verdict_empties_the_cell(self, run_folder):
        wb = ReviewWorkbook(run_folder)
        row = wb.load()[1]
        row.my_verdict = None
        wb.set_verdict(row)
        wb.save()
        assert load_workbook(run_folder / "review.xlsx")["Review"]["L3"].value is None

    def test_save_is_refused_while_excel_has_the_workbook_open_and_nothing_is_lost(self, run_folder):
        wb = ReviewWorkbook(run_folder)
        row = wb.load()[0]
        row.my_verdict = "Clear"
        wb.set_verdict(row)
        lock = run_folder / "~$review.xlsx"
        lock.write_text("")

        with pytest.raises(WorkbookOpenError):
            wb.save()
        assert wb.unsaved
        assert load_workbook(run_folder / "review.xlsx")["Review"]["L2"].value is None

        lock.unlink()
        wb.save()
        assert load_workbook(run_folder / "review.xlsx")["Review"]["L2"].value == "Clear"

    def test_columns_are_found_by_heading(self, run_folder):
        book = load_workbook(run_folder / "review.xlsx")
        book["Review"].insert_cols(1)
        book["Review"]["A1"] = "Extra"
        book.save(run_folder / "review.xlsx")

        rows = ReviewWorkbook(run_folder).load()
        assert rows[0].target == "RA01 Dec-45"
        assert rows[1].my_verdict == "Clear"

    def test_missing_workbook_or_columns_are_reported(self, tmp_path, run_folder):
        with pytest.raises(FileNotFoundError, match="run the harness replay first"):
            ReviewWorkbook(tmp_path / "nowhere")
        book = load_workbook(run_folder / "review.xlsx")
        book["Review"]["L1"] = "Mine"
        book.save(run_folder / "review.xlsx")
        with pytest.raises(ValueError, match="My verdict"):
            ReviewWorkbook(run_folder)

    def test_an_unknown_verdict_is_rejected(self, run_folder):
        wb = ReviewWorkbook(run_folder)
        row = wb.load()[0]
        row.my_verdict = "Maybe"
        with pytest.raises(ValueError):
            wb.set_verdict(row)

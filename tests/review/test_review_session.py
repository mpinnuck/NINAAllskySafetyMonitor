import pytest

from allsky_review.review_session import ReviewSession
from allsky_review.review_workbook import ReviewWorkbook
from allsky_review.row_filter import RowFilter
from allsky_review.tally import Tally
from allsky_review.traceability import requirement


def session(folder):
    return ReviewSession(ReviewWorkbook(folder))


@requirement("REQ-62")
class TestReviewSession:
    def test_starts_at_the_first_row_without_a_verdict(self, run_folder):
        s = session(run_folder)
        assert s.current.target == "RA01 Dec-45"
        assert s.position() == (1, 5)

    def test_a_verdict_is_saved_at_once_and_moves_on(self, run_folder):
        s = session(run_folder)
        assert s.record("Cloud")
        assert s.current.target == "RA02 Dec-35"
        assert session(run_folder).rows[0].my_verdict == "Cloud"

    def test_next_previous_and_next_unreviewed(self, run_folder):
        s = session(run_folder)
        assert s.next() and s.current.target == "RA02 Dec-35"
        assert s.next_unreviewed() and s.current.target == "RA03 Dec-25"
        assert s.next_unreviewed() and s.current.target == "RA05 Dec-05"
        assert not s.next()
        assert s.next_unreviewed() and s.current.target == "RA01 Dec-45"  # wraps round
        assert not s.previous()

    def test_filters(self, run_folder):
        s = session(run_folder)
        s.set_filter(RowFilter.MONITOR_CLEAR)
        assert s.current.target == "RA02 Dec-35" and s.position() == (1, 2)
        s.set_filter(RowFilter.DISAGREE)
        assert s.current.target == "RA04 Dec-15" and s.position() == (1, 1)
        s.set_filter(RowFilter.MONITOR_CLOUD)
        assert [s.current.target, s.next(), s.current.target] == ["RA01 Dec-45", True, "RA03 Dec-25"]
        assert RowFilter.ALL.following() is RowFilter.UNREVIEWED
        assert RowFilter.DISAGREE.following() is RowFilter.ALL

    def test_reviewing_the_unreviewed_filter_to_the_end(self, run_folder):
        s = session(run_folder)
        s.set_filter(RowFilter.UNREVIEWED)
        assert s.position() == (1, 3)
        for verdict in ("Cloud", "Clear", "Unsure"):
            s.record(verdict)
        assert s.current is None
        assert s.position() == (0, 0)
        assert s.tally().reviewed == 5

    def test_clearing_a_verdict_stays_on_the_row(self, run_folder):
        s = session(run_folder)
        s.next()
        s.record(None)
        assert s.current.target == "RA02 Dec-35" and s.current.my_verdict is None

    def test_tally_matches_the_summary_sheet_rules(self, run_folder):
        s = session(run_folder)
        assert s.tally() == Tally(total=5, reviewed=2, agree=1, disagree=1, false_cloud=0, missed_cloud=1)
        assert s.tally().agreement == 0.5
        s.record("Clear")  # monitor Cloud, me Clear
        s.next_unreviewed()
        s.record("Unsure")  # not counted either way
        t = s.tally()
        assert (t.reviewed, t.agree, t.disagree, t.false_cloud, t.missed_cloud) == (4, 1, 2, 1, 1)
        assert t.agreement == pytest.approx(1 / 3)

    def test_a_verdict_made_while_excel_has_the_workbook_open_is_kept_and_saved_later(self, run_folder):
        s = session(run_folder)
        lock = run_folder / "~$review.xlsx"
        lock.write_text("")
        assert not s.record("Cloud")
        assert "open in Excel" in s.save_error and s.has_unsaved
        assert s.current.target == "RA02 Dec-35"
        lock.unlink()
        assert s.record("Clear")
        assert s.save_error is None and not s.has_unsaved
        rows = session(run_folder).rows
        assert (rows[0].my_verdict, rows[1].my_verdict) == ("Cloud", "Clear")

    def test_an_unknown_verdict_is_rejected(self, run_folder):
        with pytest.raises(ValueError):
            session(run_folder).record("Maybe")

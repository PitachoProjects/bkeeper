from datetime import date, timedelta

from app.features import BookingFact
from app.synthetic import SyntheticMember
from app.train import LABEL_WINDOW_WEEKS, MIN_TENURE_WEEKS, build_snapshots


def _member_with_future_and_past_facts(join_date: date, churn_date: date | None) -> SyntheticMember:
    facts = [
        # well before the week-22 snapshot below, and outside its 4-week window (>4w back) so it
        # can't be confused with a leaked future fact
        BookingFact(session_date=join_date + timedelta(weeks=10), status="attended"),
        BookingFact(session_date=join_date + timedelta(weeks=25), status="attended"),  # future relative to snapshot below
        BookingFact(session_date=join_date + timedelta(weeks=25), status="booked", booked_at=join_date + timedelta(weeks=21)),
    ]
    return SyntheticMember(member_id=1, join_date=join_date, plan_freq=3, behavior="stable", churn_date=churn_date, facts=facts)


def test_snapshot_never_sees_attended_facts_from_after_the_snapshot_date():
    join_date = date(2026, 1, 1)
    member = _member_with_future_and_past_facts(join_date, churn_date=None)
    horizon = join_date + timedelta(weeks=22)  # snapshot taken at week 22, before the week-25 events

    rows = build_snapshots([member], horizon)
    snapshot_at_22 = next(r for r in rows if r["asof"] == join_date + timedelta(weeks=22))

    # visits_4w should NOT include the week-25 attended visit (that's in the future relative to this snapshot)
    assert snapshot_at_22["visits_4w"] == 0
    # days_since_att should reflect the week-10 visit (12 weeks before the snapshot), not anything later
    assert snapshot_at_22["days_since_att"] == 12 * 7


def test_snapshot_respects_min_tenure():
    join_date = date(2026, 1, 1)
    member = _member_with_future_and_past_facts(join_date, churn_date=None)
    horizon = join_date + timedelta(weeks=30)

    rows = build_snapshots([member], horizon)
    assert all((r["asof"] - join_date).days // 7 >= MIN_TENURE_WEEKS for r in rows)


def test_label_is_one_only_within_the_28_day_window_after_snapshot():
    join_date = date(2026, 1, 1)
    churn_date = join_date + timedelta(weeks=30)
    member = _member_with_future_and_past_facts(join_date, churn_date=churn_date)
    horizon = churn_date

    rows = build_snapshots([member], horizon)
    by_week = {(r["asof"] - join_date).days // 7: r["label"] for r in rows}

    # snapshot exactly LABEL_WINDOW_WEEKS before churn -> label 1
    assert by_week[30 - LABEL_WINDOW_WEEKS] == 1
    # snapshot far before churn -> label 0
    assert by_week[12] == 0


def test_no_snapshots_created_on_or_after_churn():
    join_date = date(2026, 1, 1)
    churn_date = join_date + timedelta(weeks=20)
    member = _member_with_future_and_past_facts(join_date, churn_date=churn_date)
    horizon = join_date + timedelta(weeks=30)

    rows = build_snapshots([member], horizon)
    assert all(r["asof"] < churn_date for r in rows)

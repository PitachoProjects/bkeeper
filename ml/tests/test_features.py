from datetime import date, timedelta

from app.features import BookingFact, build_features


def test_tenure_and_days_since_att():
    join_date = date(2026, 1, 1)
    asof = date(2026, 6, 1)  # 152 days later
    facts = [BookingFact(session_date=asof - timedelta(days=10), status="attended")]

    feats = build_features(join_date, 3, asof, facts)

    assert feats["tenure_w"] == 152 // 7
    assert feats["days_since_att"] == 10


def test_visits_4w_counts_only_recent_attended():
    join_date = date(2026, 1, 1)
    asof = date(2026, 6, 1)
    facts = [
        BookingFact(session_date=asof - timedelta(days=5), status="attended"),
        BookingFact(session_date=asof - timedelta(days=10), status="attended"),
        BookingFact(session_date=asof - timedelta(days=40), status="attended"),  # outside 4w
        BookingFact(session_date=asof - timedelta(days=3), status="no_show"),  # not attended
    ]

    feats = build_features(join_date, 3, asof, facts)
    assert feats["visits_4w"] == 2


def test_upcoming_7d_true_when_booked_within_window():
    join_date = date(2026, 1, 1)
    asof = date(2026, 6, 1)
    facts = [BookingFact(session_date=asof + timedelta(days=3), status="booked", booked_at=asof)]

    feats = build_features(join_date, 3, asof, facts)
    assert feats["upcoming_7d"] == 1.0


def test_upcoming_7d_false_when_no_future_booking():
    join_date = date(2026, 1, 1)
    asof = date(2026, 6, 1)
    feats = build_features(join_date, 3, asof, [])
    assert feats["upcoming_7d"] == 0.0


def test_noshow_rate_8w():
    join_date = date(2026, 1, 1)
    asof = date(2026, 6, 1)
    facts = [
        BookingFact(session_date=asof - timedelta(days=5), status="attended"),
        BookingFact(session_date=asof - timedelta(days=10), status="no_show"),
        BookingFact(session_date=asof - timedelta(days=15), status="late_cancel"),
        BookingFact(session_date=asof - timedelta(days=20), status="attended"),
    ]

    feats = build_features(join_date, 3, asof, facts)
    assert feats["noshow_rate_8w"] == 0.5


def _bucket_mid(asof: date, lag_weeks: int, bucket_index: int) -> date:
    """A date safely inside bucket `bucket_index` of an (asof, lag_weeks)-anchored weekly window
    (bucket 0 = the most recent, [asof - lag_weeks - 1w, asof - lag_weeks)) — mirrors
    `_weekly_counts`'s own bucketing so fixtures can't land on an edge by accident."""
    bucket_end = asof - timedelta(weeks=lag_weeks) - timedelta(weeks=bucket_index)
    return bucket_end - timedelta(days=3)


def test_zero_and_low_weeks_6():
    join_date = date(2026, 1, 1)
    asof = date(2026, 8, 1)
    # The unlagged "recent 6 weeks" window and the lagged-4w "8-week baseline" window overlap over
    # weeks 4-6 back (same historical facts feed both), so build one consistent picture: buckets 0-1
    # (most recent) empty, buckets 2-3 low (1 visit), buckets 4+ steady at 3 visits/week — which is
    # also what the baseline sees since the only facts more than 4 weeks old are the steady-3 ones.
    facts = []
    counts = {0: 0, 1: 0, 2: 1, 3: 1, 4: 3, 5: 3, 6: 3, 7: 3, 8: 3, 9: 3, 10: 3, 11: 3}
    for i, count in counts.items():
        for _ in range(count):
            facts.append(BookingFact(session_date=_bucket_mid(asof, 0, i), status="attended"))

    feats = build_features(join_date, 3, asof, facts)
    assert feats["base_rate_8w"] == 3.0  # lagged buckets 0-7 = unlagged buckets 4-11, all steady-3
    assert feats["zero_weeks_6"] == 2
    assert feats["low_weeks_6"] == 4  # low_weeks_6 counts <0.5*baseline, which includes the zero weeks too


def test_base_rate_excludes_last_4_weeks_to_avoid_leaking_the_current_dip():
    join_date = date(2026, 1, 1)
    asof = date(2026, 8, 1)
    facts = [BookingFact(session_date=_bucket_mid(asof, 4, i), status="attended") for i in range(8)]
    # total dropout in the most recent 4 weeks (the "current gap" that a rule would flag)
    # — must NOT pull the baseline down, since the baseline should reflect *before* the dip.

    feats = build_features(join_date, 3, asof, facts)
    assert feats["base_rate_8w"] == 1.0

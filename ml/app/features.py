"""
Feature computation for the churn model (plan §8's 16-feature list).

This is the *only* place these features are computed — both train.py and serve.py import it, so
there is exactly one implementation to keep in sync with itself. That sidesteps the plan's
C#-vs-Python "feature parity" test: instead of computing the same features twice (once in .NET for
training, once in Python for serving) and testing they agree, the .NET side never computes features
at all — it ships raw booking facts to this service and this module is the single source of truth.
See docs/DECISIONS.md for the tradeoff (a network hop at serve time, zero drift risk).
"""

from __future__ import annotations

import math
from dataclasses import dataclass, field
from datetime import date, timedelta

FEATURE_NAMES = [
    "tenure_w",
    "plan_ord",
    "visits_4w",
    "base_rate_8w",
    "ratio_4w_8w",
    "slope_8w",
    "zero_weeks_6",
    "base_rate_26w",
    "ratio_2w_26w",
    "low_weeks_6",
    "days_since_att",
    "days_since_book",
    "upcoming_7d",
    "noshow_rate_8w",
    "window_shift",
    "type_shift",
]


@dataclass
class BookingFact:
    session_date: date
    status: str  # attended | no_show | late_cancel | cancelled | booked
    booked_at: date | None = None
    window: str | None = None  # early | morning | lunch | afternoon | evening
    type_weights: dict[str, float] = field(default_factory=dict)


def _weekly_counts(dates: list[date], asof: date, weeks: int, lag_weeks: int = 0) -> list[int]:
    """Attended visits per week, most-recent-first, ending `lag_weeks` weeks before asof."""
    end = asof - timedelta(weeks=lag_weeks)
    counts = []
    for w in range(weeks):
        week_end = end - timedelta(weeks=w)
        week_start = week_end - timedelta(days=7)
        counts.append(sum(1 for d in dates if week_start <= d < week_end))
    return counts


def _slope(values: list[float]) -> float:
    """Simple linear regression slope over equally-spaced points (index 0 = most recent)."""
    n = len(values)
    if n < 2:
        return 0.0
    xs = list(range(n))
    x_mean = sum(xs) / n
    y_mean = sum(values) / n
    num = sum((x - x_mean) * (y - y_mean) for x, y in zip(xs, values))
    den = sum((x - x_mean) ** 2 for x in xs)
    return 0.0 if den == 0 else num / den


def _jensen_shannon(p: dict[str, float], q: dict[str, float]) -> float:
    keys = set(p) | set(q)
    if not keys:
        return 0.0
    p_total = sum(p.get(k, 0) for k in keys) or 1.0
    q_total = sum(q.get(k, 0) for k in keys) or 1.0
    pn = {k: p.get(k, 0) / p_total for k in keys}
    qn = {k: q.get(k, 0) / q_total for k in keys}
    m = {k: (pn[k] + qn[k]) / 2 for k in keys}

    def kl(a, b):
        return sum(a[k] * math.log2(a[k] / b[k]) for k in keys if a[k] > 0)

    return (kl(pn, m) + kl(qn, m)) / 2


def build_features(join_date: date, plan_freq_per_week: float | None, asof: date, facts: list[BookingFact]) -> dict[str, float]:
    attended = sorted(f.session_date for f in facts if f.status == "attended")
    tenure_days = (asof - join_date).days
    tenure_w = max(0, tenure_days) // 7

    weekly_8 = _weekly_counts(attended, asof, weeks=8, lag_weeks=4)
    weekly_26 = _weekly_counts(attended, asof, weeks=26, lag_weeks=4)
    weekly_6_recent = _weekly_counts(attended, asof, weeks=6, lag_weeks=0)

    base_rate_8w = sum(weekly_8) / 8 if weekly_8 else 0.0
    base_rate_26w = sum(weekly_26) / 26 if weekly_26 else 0.0

    visits_4w = sum(1 for d in attended if asof - timedelta(weeks=4) <= d <= asof)
    visits_2w = sum(1 for d in attended if asof - timedelta(weeks=2) <= d <= asof)

    ratio_4w_8w = (visits_4w / 4) / base_rate_8w if base_rate_8w > 0 else 0.0
    ratio_2w_26w = (visits_2w / 2) / base_rate_26w if base_rate_26w > 0 else 0.0

    zero_weeks_6 = sum(1 for c in weekly_6_recent if c == 0)
    low_weeks_6 = sum(1 for c in weekly_6_recent if base_rate_8w > 0 and c < 0.5 * base_rate_8w)

    slope_8w = _slope(list(reversed(weekly_8)))  # oldest -> newest for a meaningful trend sign

    last_att = attended[-1] if attended else None
    days_since_att = (asof - last_att).days if last_att else 999

    booked_dates = sorted(f.booked_at for f in facts if f.booked_at is not None and f.booked_at <= asof)
    days_since_book = (asof - booked_dates[-1]).days if booked_dates else 999

    upcoming_7d = 1.0 if any(f.status == "booked" and asof < f.session_date <= asof + timedelta(days=7) for f in facts) else 0.0

    last_8w_facts = [f for f in facts if asof - timedelta(weeks=8) <= f.session_date <= asof]
    bookings_8w = len(last_8w_facts)
    noshows_8w = sum(1 for f in last_8w_facts if f.status in ("no_show", "late_cancel"))
    noshow_rate_8w = noshows_8w / bookings_8w if bookings_8w > 0 else 0.0

    window_shift = _compute_window_shift(facts, asof)
    type_shift = _compute_type_shift(facts, asof)

    return {
        "tenure_w": float(tenure_w),
        "plan_ord": float(plan_freq_per_week or 0),
        "visits_4w": float(visits_4w),
        "base_rate_8w": base_rate_8w,
        "ratio_4w_8w": ratio_4w_8w,
        "slope_8w": slope_8w,
        "zero_weeks_6": float(zero_weeks_6),
        "base_rate_26w": base_rate_26w,
        "ratio_2w_26w": ratio_2w_26w,
        "low_weeks_6": float(low_weeks_6),
        "days_since_att": float(days_since_att),
        "days_since_book": float(days_since_book),
        "upcoming_7d": upcoming_7d,
        "noshow_rate_8w": noshow_rate_8w,
        "window_shift": window_shift,
        "type_shift": type_shift,
    }


def _compute_window_shift(facts: list[BookingFact], asof: date) -> float:
    recent = [f.window for f in facts if f.status == "attended" and asof - timedelta(weeks=4) <= f.session_date <= asof and f.window]
    prior = [f.window for f in facts if f.status == "attended" and asof - timedelta(weeks=16) <= f.session_date < asof - timedelta(weeks=4) and f.window]
    if not recent or not prior:
        return 0.0

    def usual(windows: list[str]) -> str:
        return max(set(windows), key=windows.count)

    usual_window = usual(prior)
    recent_share = recent.count(usual_window) / len(recent)
    prior_share = prior.count(usual_window) / len(prior)
    return max(0.0, prior_share - recent_share)


def _compute_type_shift(facts: list[BookingFact], asof: date) -> float:
    def type_mix(start_weeks: int, end_weeks: int) -> dict[str, float]:
        mix: dict[str, float] = {}
        for f in facts:
            if f.status != "attended" or not (asof - timedelta(weeks=start_weeks) <= f.session_date <= asof - timedelta(weeks=end_weeks)):
                continue
            for t, w in f.type_weights.items():
                mix[t] = mix.get(t, 0) + w
        return mix

    recent_mix = type_mix(6, 0)
    prior_mix = type_mix(18, 6)
    if not recent_mix or not prior_mix:
        return 0.0
    return _jensen_shannon(recent_mix, prior_mix)

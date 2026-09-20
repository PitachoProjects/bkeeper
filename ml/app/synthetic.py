"""
Synthetic member/booking generator for training and backtesting when no real export exists yet
(plan §8: "numbers will differ on real data — the first real backtest decides thresholds").

This is an independent, simplified generator — not a reproduction of the plan's own
`blackkeeper_prototype.py` companion script, which wasn't provided alongside the plan. It exists to
let the training/backtest pipeline run and be sanity-checked end-to-end before any real export
exists, not to make production-grade churn-rate claims.
"""

from __future__ import annotations

import random
from dataclasses import dataclass
from datetime import date, timedelta

from app.features import BookingFact

WINDOWS = ["early", "morning", "lunch", "afternoon", "evening"]
TYPES = ["strength", "metcon", "gymnastics", "endurance", "hybrid"]
BEHAVIORS = ["stable", "gradual_fade", "sudden", "early_dropout"]
BEHAVIOR_WEIGHTS = [0.62, 0.18, 0.10, 0.10]


@dataclass
class SyntheticMember:
    member_id: int
    join_date: date
    plan_freq: int
    behavior: str
    churn_date: date | None
    facts: list[BookingFact]


def generate(n_members: int = 600, weeks: int = 104, seed: int = 42, start: date = date(2024, 1, 1)) -> list[SyntheticMember]:
    rng = random.Random(seed)
    members: list[SyntheticMember] = []

    for member_id in range(n_members):
        join_week = rng.randint(0, max(1, weeks - 60))
        join_date = start + timedelta(weeks=join_week)
        plan_freq = rng.choice([2, 3, 4, 5])
        behavior = rng.choices(BEHAVIORS, BEHAVIOR_WEIGHTS)[0]
        baseline_rate = plan_freq * rng.uniform(0.45, 0.85)
        usual_window = rng.choice(WINDOWS)
        usual_types = rng.sample(TYPES, k=2)

        remaining_weeks = weeks - join_week
        churn_offset = None
        if behavior != "stable" and remaining_weeks > 10:
            churn_offset = rng.randint(8, remaining_weeks - 1)
        churn_date = join_date + timedelta(weeks=churn_offset) if churn_offset is not None else None

        facts: list[BookingFact] = []
        for w in range(remaining_weeks):
            week_start = join_date + timedelta(weeks=w)
            if churn_date is not None and week_start >= churn_date:
                break

            rate = _weekly_rate(behavior, baseline_rate, w, churn_offset)
            visits = min(7, max(0, round(rng.gauss(rate, max(0.3, rate * 0.3)))))

            for _ in range(visits):
                day_offset = rng.randint(0, 6)
                session_date = week_start + timedelta(days=day_offset)
                window = usual_window if rng.random() < 0.75 else rng.choice(WINDOWS)
                type_choice = rng.choice(usual_types) if rng.random() < 0.7 else rng.choice(TYPES)
                is_noshow = rng.random() < (0.15 if behavior == "sudden" and churn_offset and w > churn_offset - 3 else 0.04)
                facts.append(BookingFact(
                    session_date=session_date,
                    status="no_show" if is_noshow else "attended",
                    booked_at=session_date - timedelta(days=1),
                    window=window,
                    type_weights={type_choice: 1.0},
                ))

            if rng.random() < 0.6 and (churn_date is None or week_start + timedelta(weeks=1) < churn_date):
                upcoming_date = week_start + timedelta(days=rng.randint(7, 10))
                facts.append(BookingFact(session_date=upcoming_date, status="booked", booked_at=week_start))

        members.append(SyntheticMember(member_id, join_date, plan_freq, behavior, churn_date, facts))

    return members


def _weekly_rate(behavior: str, baseline: float, week_index: int, churn_offset: int | None) -> float:
    if behavior == "stable" or churn_offset is None:
        return baseline

    weeks_to_churn = churn_offset - week_index
    if behavior == "gradual_fade":
        if weeks_to_churn <= 8:
            return baseline * max(0.0, weeks_to_churn / 8)
        return baseline
    if behavior == "sudden":
        return baseline if weeks_to_churn > 1 else baseline * 0.2
    if behavior == "early_dropout":
        return baseline * 1.3 if week_index <= 2 else baseline * 0.15
    return baseline

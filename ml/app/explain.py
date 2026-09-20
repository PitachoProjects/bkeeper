"""
Top-3 driver explanations (plan §8): SHAP values on the LightGBM component, mapped through a
whitelist to readable phrases, and only shown when they push risk *up* — a driver that's protecting
the member (e.g. a very recent visit) is never surfaced as a "reason they're at risk".
"""

from __future__ import annotations

PHRASES: dict[str, str] = {
    "days_since_att": "hasn't attended in {value:.0f} days",
    "ratio_4w_8w": "last 4 weeks are only {value:.0%} of their usual pace",
    "ratio_2w_26w": "last 2 weeks are only {value:.0%} of their long-term average",
    "zero_weeks_6": "{value:.0f} weeks with zero visits in the last 6",
    "low_weeks_6": "{value:.0f} low-attendance weeks in the last 6",
    "noshow_rate_8w": "{value:.0%} no-show/late-cancel rate over 8 weeks",
    "slope_8w": "attendance has been trending down",
    "window_shift": "shifted away from their usual class time",
    "type_shift": "shifted away from their usual workout type",
    "days_since_book": "hasn't booked a class in {value:.0f} days",
    "upcoming_7d": "no upcoming booking in the next 7 days",
    "base_rate_8w": "baseline attendance has dropped",
}


def top_reasons(shap_values: dict[str, float], feature_values: dict[str, float], top_k: int = 3) -> list[str]:
    """shap_values: feature -> SHAP contribution to the positive (churn) class."""
    risky = [(f, v) for f, v in shap_values.items() if v > 0 and f in PHRASES]
    risky.sort(key=lambda x: x[1], reverse=True)

    reasons = []
    for feature, _ in risky[:top_k]:
        value = feature_values.get(feature, 0.0)
        reasons.append(PHRASES[feature].format(value=value))
    return reasons

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


def top_reasons_linear(bundle: dict, feature_values: dict[str, float], top_k: int = 3) -> list[str]:
    """Stage B's interpretability: contribution = the plain logistic regression's standardized
    coefficient times this member's standardized feature value — a statistical, per-member
    decomposition of the log-odds, not a SHAP explanation. Uses `coefficients_model` (the plain,
    uncalibrated fit kept in the bundle for exactly this) rather than the calibrated ensemble, whose
    per-fold coefficients aren't a single readable vector. Same risk-direction-only filtering and
    phrase whitelist as `top_reasons` so the two models' reasons read the same way to a coach."""
    feature_names = bundle["feature_names"]
    z = bundle["scaler"].transform([[feature_values[f] for f in feature_names]])[0]
    coefs = bundle["coefficients_model"].coef_[0]

    contributions = [(f, float(coefs[i] * z[i])) for i, f in enumerate(feature_names)]
    risky = [(f, v) for f, v in contributions if v > 0 and f in PHRASES]
    risky.sort(key=lambda x: x[1], reverse=True)

    reasons = []
    for feature, _ in risky[:top_k]:
        reasons.append(PHRASES[feature].format(value=feature_values.get(feature, 0.0)))
    return reasons

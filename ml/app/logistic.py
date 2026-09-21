"""
Stage B baseline (product roadmap: A = rules, B = interpretable statistical model, C = gradient
boosting). Trains a standalone, calibrated logistic regression — a genuinely separate model from
the Stage C LightGBM ensemble in train.py, not a replacement for it, registered under its own
`model_type` ("logistic_regression") so the two can be compared side by side (see
`ml/backtest_compare.py` and docs/RUNBOOK.md).

Reuses train.py's own snapshot-building and strict time-based train/test split rather than
re-implementing either — same point-in-time correctness guarantees `tests/test_no_leakage.py`
already checks for the shared feature pipeline (app/features.py). Run as a script:

    python -m app.logistic --version v1
"""

from __future__ import annotations

import argparse
import json
from dataclasses import dataclass
from datetime import date, timedelta

import numpy as np
from sklearn.calibration import CalibratedClassifierCV
from sklearn.linear_model import LogisticRegression
from sklearn.model_selection import StratifiedKFold
from sklearn.preprocessing import StandardScaler

from app import model_registry
from app.features import FEATURE_NAMES
from app.metrics import evaluate_binary
from app.synthetic import generate
from app.train import build_snapshots, temporal_split, _matrix

MODEL_TYPE = "logistic_regression"
MIN_POSITIVES_TO_CALIBRATE = 20  # below this, CV folds for calibration would be too thin to trust


@dataclass
class CoefficientReport:
    feature: str
    coefficient: float
    odds_ratio: float
    direction: str  # increases_risk | decreases_risk
    abs_rank: int


def coefficient_report(model: LogisticRegression, feature_names: list[str]) -> list[dict]:
    """Coefficients on the *standardized* features, so magnitude is directly comparable across
    features (plan §8's "statistically important features" — this is Stage B's whole point, a
    plain positive/negative direction and relative magnitude, not a SHAP black box)."""
    coefs = model.coef_[0]
    order = np.argsort(-np.abs(coefs))
    reports = []
    for rank, idx in enumerate(order):
        c = float(coefs[idx])
        reports.append(CoefficientReport(
            feature=feature_names[idx],
            coefficient=c,
            odds_ratio=float(np.exp(c)),
            direction="increases_risk" if c > 0 else "decreases_risk",
            abs_rank=rank,
        ))
    return [r.__dict__ for r in reports]


def _fit_calibrated(x_train_scaled: np.ndarray, y_train: np.ndarray, C: float) -> tuple[object, LogisticRegression]:
    """Returns (probability_model, coefficients_model). Calibration wraps a fresh copy of the same
    logistic regression per CV fold, so its coefficients aren't a single readable vector; a plain
    logistic regression fit on the full training set is kept alongside, only for the coefficient
    report, while the calibrated model is what actually produces the probabilities."""
    plain = LogisticRegression(max_iter=1000, class_weight="balanced", C=C, penalty="l2")
    plain.fit(x_train_scaled, y_train)

    n_pos = int(y_train.sum())
    n_neg = len(y_train) - n_pos
    if n_pos < MIN_POSITIVES_TO_CALIBRATE or n_neg < MIN_POSITIVES_TO_CALIBRATE:
        return plain, plain

    cv = StratifiedKFold(n_splits=min(5, n_pos), shuffle=False)
    calibrated = CalibratedClassifierCV(
        LogisticRegression(max_iter=1000, class_weight="balanced", C=C, penalty="l2"),
        method="sigmoid",
        cv=cv,
    )
    calibrated.fit(x_train_scaled, y_train)
    return calibrated, plain


def train_and_evaluate(train_rows: list[dict], test_rows: list[dict], *, C: float = 1.0):
    x_train, y_train = _matrix(train_rows)
    x_test, y_test = _matrix(test_rows)

    scaler = StandardScaler().fit(x_train)
    x_train_scaled = scaler.transform(x_train)
    x_test_scaled = scaler.transform(x_test)

    proba_model, coef_model = _fit_calibrated(x_train_scaled, y_train, C)
    p_test = proba_model.predict_proba(x_test_scaled)[:, 1]

    metrics = evaluate_binary(y_test, p_test)
    metrics["calibrated"] = proba_model is not coef_model
    metrics["coefficients"] = coefficient_report(coef_model, FEATURE_NAMES)

    bundle = {
        "scaler": scaler,
        "model": proba_model,
        "coefficients_model": coef_model,
        "feature_names": FEATURE_NAMES,
    }
    return bundle, metrics


def score(bundle: dict, feats: dict[str, float]) -> float:
    x = np.array([[feats[f] for f in bundle["feature_names"]]])
    x_scaled = bundle["scaler"].transform(x)
    return float(bundle["model"].predict_proba(x_scaled)[0, 1])


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--version", default="v1")
    parser.add_argument("--n-members", type=int, default=600)
    parser.add_argument("--weeks", type=int, default=104)
    parser.add_argument("--seed", type=int, default=42)
    parser.add_argument("--C", type=float, default=1.0, help="Inverse L2 regularization strength")
    args = parser.parse_args()

    start = date(2024, 1, 1)
    members = generate(n_members=args.n_members, weeks=args.weeks, seed=args.seed, start=start)
    horizon = start + timedelta(weeks=args.weeks)
    rows = build_snapshots(members, horizon)
    print(f"Built {len(rows)} snapshots from {len(members)} synthetic members (base rate {np.mean([r['label'] for r in rows]):.3%})")

    train_rows, test_rows = temporal_split(rows)
    print(f"Train: {len(train_rows)} snapshots, Test: {len(test_rows)} snapshots (same temporal split as train.py)")

    bundle, metrics = train_and_evaluate(train_rows, test_rows, C=args.C)
    print(json.dumps(metrics, indent=2, default=str))

    metadata = model_registry.save(args.version, bundle, metrics, FEATURE_NAMES, model_type=MODEL_TYPE)
    print(f"Saved model {metadata.model_type}/{metadata.version} ({metadata.trained_at})")


if __name__ == "__main__":
    main()

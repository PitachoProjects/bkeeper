"""
Training + backtest harness (plan §8/Week8). Run as a script:

    python -m app.train --version v1

Builds weekly snapshots from the synthetic generator (or a real export once one exists — see
`build_snapshots`), does a strict temporal train/test split (never random — the label looks 28 days
forward, so a random split would leak), trains a logistic regression + LightGBM ensemble, evaluates
it, and saves the result to the model registry.
"""

from __future__ import annotations

import argparse
import json
from datetime import date, timedelta

import lightgbm as lgb
import numpy as np
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import average_precision_score, roc_auc_score
from sklearn.preprocessing import StandardScaler

from app import model_registry
from app.features import FEATURE_NAMES, build_features
from app.metrics import calibration_bins, precision_recall_at_k
from app.synthetic import generate

GAP_WEEKS = 4
LABEL_WINDOW_WEEKS = 4
MIN_TENURE_WEEKS = 12


def build_snapshots(members, horizon: date):
    """One row per (member, weekly snapshot) where the member is tenured and still active."""
    rows = []
    for m in members:
        w = 0
        while True:
            asof = m.join_date + timedelta(weeks=w)
            if asof > horizon:
                break
            if m.churn_date is not None and asof >= m.churn_date:
                break
            w += 1

            tenure_weeks = (asof - m.join_date).days // 7
            if tenure_weeks < MIN_TENURE_WEEKS:
                continue

            # No-leakage cut: only facts knowable as of `asof` — a past outcome (attended/no_show)
            # must have already happened; a future *booking* is fine, since the booking itself
            # happened by `asof` even though the class is later.
            visible_facts = [
                f for f in m.facts
                if (f.status == "booked" and f.booked_at is not None and f.booked_at <= asof)
                or (f.status != "booked" and f.session_date <= asof)
            ]

            label = 1 if m.churn_date is not None and asof < m.churn_date <= asof + timedelta(weeks=LABEL_WINDOW_WEEKS) else 0
            feats = build_features(m.join_date, float(m.plan_freq), asof, visible_facts)
            rows.append({"member_id": m.member_id, "asof": asof, "label": label, **feats})

    return rows


def temporal_split(rows: list[dict], test_fraction: float = 0.25):
    """Split by calendar date, not by snapshot count — active-member density (and so snapshot
    count) piles up toward the end of the simulated horizon, so a count-percentile split lands the
    cutoff too close to the horizon edge and starves the test period of room for 4-week-forward
    labels to even materialize."""
    min_date = min(r["asof"] for r in rows)
    max_date = max(r["asof"] for r in rows)
    split_date = min_date + (max_date - min_date) * (1 - test_fraction)
    gap_end = split_date + timedelta(weeks=GAP_WEEKS)

    train = [r for r in rows if r["asof"] <= split_date]
    test = [r for r in rows if r["asof"] > gap_end]
    return train, test


def _matrix(rows: list[dict]):
    x = np.array([[r[f] for f in FEATURE_NAMES] for r in rows])
    y = np.array([r["label"] for r in rows])
    return x, y


def train_and_evaluate(train_rows: list[dict], test_rows: list[dict]):
    x_train, y_train = _matrix(train_rows)
    x_test, y_test = _matrix(test_rows)

    scaler = StandardScaler().fit(x_train)
    x_train_scaled = scaler.transform(x_train)
    x_test_scaled = scaler.transform(x_test)

    logreg = LogisticRegression(max_iter=1000, class_weight="balanced")
    logreg.fit(x_train_scaled, y_train)

    lgbm = lgb.LGBMClassifier(max_depth=3, min_child_samples=120, reg_lambda=1.0, n_estimators=200, verbosity=-1)
    lgbm.fit(x_train, y_train)

    p_logreg = logreg.predict_proba(x_test_scaled)[:, 1]
    p_lgbm = lgbm.predict_proba(x_test)[:, 1]
    p_ensemble = (p_logreg + p_lgbm) / 2

    metrics = evaluate(y_test, p_logreg, p_lgbm, p_ensemble)
    bundle = {"scaler": scaler, "logreg": logreg, "lgbm": lgbm, "feature_names": FEATURE_NAMES}
    return bundle, metrics


def evaluate(y_test, p_logreg, p_lgbm, p_ensemble) -> dict:
    base_rate = float(np.mean(y_test))
    auc = {
        "logreg": roc_auc_score(y_test, p_logreg) if base_rate not in (0, 1) else None,
        "lgbm": roc_auc_score(y_test, p_lgbm) if base_rate not in (0, 1) else None,
        "ensemble": roc_auc_score(y_test, p_ensemble) if base_rate not in (0, 1) else None,
    }
    pr_auc = average_precision_score(y_test, p_ensemble) if base_rate not in (0, 1) else None

    order = np.argsort(-p_ensemble)
    top5_n = max(1, int(len(y_test) * 0.05))
    top5_idx = order[:top5_n]
    lift_top5 = (float(np.mean(y_test[top5_idx])) / base_rate) if base_rate > 0 else None

    degenerate = base_rate in (0, 1)
    return {
        "base_rate": base_rate,
        "n_test": len(y_test),
        "auc": auc,
        "pr_auc": pr_auc,
        "lift_top5pct": lift_top5,
        # Same precision/recall-at-k and calibration shape app/logistic.py reports (app/metrics.py),
        # on the ensemble probability — so backtest_compare.py can put both models side by side.
        "precision_recall_at_k": [] if degenerate else [precision_recall_at_k(y_test, p_ensemble, k) for k in (0.02, 0.05, 0.10)],
        "calibration": [] if degenerate else calibration_bins(y_test, p_ensemble),
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--version", default="v1")
    parser.add_argument("--n-members", type=int, default=600)
    parser.add_argument("--weeks", type=int, default=104)
    parser.add_argument("--seed", type=int, default=42)
    args = parser.parse_args()

    start = date(2024, 1, 1)
    members = generate(n_members=args.n_members, weeks=args.weeks, seed=args.seed, start=start)
    # ponytail-caught bug: this used to read `members[0].join_date` as the horizon anchor, but
    # member 0's own join week is itself randomized (can be 0-100+ weeks into the generator's
    # timeline) — silently shifting/truncating the horizon and starving the test period of any
    # late-calendar churn events. Anchor on the actual generator `start` instead.
    horizon = start + timedelta(weeks=args.weeks)
    rows = build_snapshots(members, horizon)
    print(f"Built {len(rows)} snapshots from {len(members)} synthetic members (base rate {np.mean([r['label'] for r in rows]):.3%})")

    train_rows, test_rows = temporal_split(rows)
    print(f"Train: {len(train_rows)} snapshots, Test: {len(test_rows)} snapshots (temporal split, {GAP_WEEKS}w gap)")

    bundle, metrics = train_and_evaluate(train_rows, test_rows)
    print(json.dumps(metrics, indent=2, default=str))

    metadata = model_registry.save(args.version, bundle, metrics, FEATURE_NAMES)
    print(f"Saved model {metadata.version} ({metadata.trained_at})")


if __name__ == "__main__":
    main()

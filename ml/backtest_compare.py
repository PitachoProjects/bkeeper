"""
Side-by-side backtest: Stage B (logistic regression, app/logistic.py) vs. Stage C (LightGBM
ensemble, app/train.py) on the *same* snapshots and the *same* time-based train/test split, so
their precision/recall/ROC-AUC/PR-AUC/calibration numbers are directly comparable. Neither model
is retrained differently for this — it just calls each module's own `train_and_evaluate` once and
prints both metrics dicts next to each other, plus the logistic model's coefficients (Stage B's
interpretability, absent from the LightGBM/SHAP side).

Run from `ml/`:

    python backtest_compare.py --version v1

See docs/RUNBOOK.md for how to read the output. Uses the synthetic generator until a real export
exists (docs/OPEN_QUESTIONS.md) — treat the numbers as an engineering sanity check, not a
performance claim.
"""

from __future__ import annotations

import argparse
import json
from datetime import date, timedelta

import numpy as np

from app import logistic, model_registry, train
from app.features import FEATURE_NAMES
from app.synthetic import generate


def run(n_members: int, weeks: int, seed: int, version: str, save: bool) -> dict:
    start = date(2024, 1, 1)
    members = generate(n_members=n_members, weeks=weeks, seed=seed, start=start)
    horizon = start + timedelta(weeks=weeks)
    rows = train.build_snapshots(members, horizon)
    train_rows, test_rows = train.temporal_split(rows)

    print(f"Snapshots: {len(rows)} total, {len(train_rows)} train, {len(test_rows)} test "
          f"(base rate {np.mean([r['label'] for r in rows]):.3%}, same split for both models)")

    lgbm_bundle, lgbm_metrics = train.train_and_evaluate(train_rows, test_rows)
    logreg_bundle, logreg_metrics = logistic.train_and_evaluate(train_rows, test_rows)

    if save:
        lgbm_meta = model_registry.save(version, lgbm_bundle, lgbm_metrics, FEATURE_NAMES)
        logreg_meta = model_registry.save(version, logreg_bundle, logreg_metrics, FEATURE_NAMES, model_type=logistic.MODEL_TYPE)
        print(f"Saved {lgbm_meta.model_type}/{lgbm_meta.version} and {logreg_meta.model_type}/{logreg_meta.version}")

    return {"lightgbm_ensemble": lgbm_metrics, "logistic_regression": logreg_metrics}


def summarize(comparison: dict) -> str:
    lgbm, logreg = comparison["lightgbm_ensemble"], comparison["logistic_regression"]
    lines = [
        f"{'Metric':<28}{'LightGBM ensemble':<22}{'Logistic regression':<22}",
        "-" * 72,
        f"{'ROC-AUC (ensemble)':<28}{lgbm['auc']['ensemble']:<22.4f}{logreg['roc_auc']:<22.4f}",
        f"{'PR-AUC':<28}{lgbm['pr_auc']:<22.4f}{logreg['pr_auc']:<22.4f}",
        f"{'Lift, top 5%':<28}{lgbm['lift_top5pct']:<22.2f}{logreg['lift_top5pct']:<22.2f}",
    ]
    lgbm_by_k = {k["k_fraction"]: k for k in lgbm["precision_recall_at_k"]}
    for k in logreg["precision_recall_at_k"]:
        frac = k["k_fraction"]
        lgbm_k = lgbm_by_k.get(frac, {"precision": float("nan"), "recall": float("nan")})
        lines.append(f"{f'Precision @ top {frac:.0%}':<28}{lgbm_k['precision']:<22.4f}{k['precision']:<22.4f}")
        lines.append(f"{f'Recall @ top {frac:.0%}':<28}{lgbm_k['recall']:<22.4f}{k['recall']:<22.4f}")
    lines.append("")
    lines.append("Logistic regression is the only one of the two with a readable coefficient")
    lines.append("table (see the 'coefficients' key in its metrics JSON) — that's Stage B's point.")
    return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--version", default="v1")
    parser.add_argument("--n-members", type=int, default=600)
    parser.add_argument("--weeks", type=int, default=104)
    parser.add_argument("--seed", type=int, default=42)
    parser.add_argument("--save", action="store_true", help="Register both trained models (registry, not just print).")
    parser.add_argument("--json", action="store_true", help="Print the full metrics JSON instead of the summary table.")
    args = parser.parse_args()

    comparison = run(args.n_members, args.weeks, args.seed, args.version, args.save)

    if args.json:
        print(json.dumps(comparison, indent=2, default=str))
    else:
        print()
        print(summarize(comparison))


if __name__ == "__main__":
    main()

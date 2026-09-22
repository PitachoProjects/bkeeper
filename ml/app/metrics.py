"""
Shared evaluation metrics so Stage B (logistic regression, app/logistic.py) and Stage C
(LightGBM ensemble, app/train.py) are scored the same way and are directly comparable — precision,
recall, ROC-AUC, PR-AUC, lift and calibration, all computed from one probability vector and one
label vector.
"""

from __future__ import annotations

import numpy as np
from sklearn.metrics import (
    average_precision_score,
    precision_score,
    recall_score,
    roc_auc_score,
)


def precision_recall_at_threshold(y_true: np.ndarray, p: np.ndarray, threshold: float) -> dict:
    predicted_positive = p >= threshold
    return {
        "threshold": threshold,
        "n_flagged": int(predicted_positive.sum()),
        "precision": float(precision_score(y_true, predicted_positive, zero_division=0)),
        "recall": float(recall_score(y_true, predicted_positive, zero_division=0)),
    }


def precision_recall_at_k(y_true: np.ndarray, p: np.ndarray, k_fraction: float) -> dict:
    n = len(y_true)
    k = max(1, int(n * k_fraction))
    order = np.argsort(-p)
    top_k = order[:k]
    predicted_positive = np.zeros(n, dtype=bool)
    predicted_positive[top_k] = True
    return {
        "k_fraction": k_fraction,
        "n_flagged": k,
        "precision": float(precision_score(y_true, predicted_positive, zero_division=0)),
        "recall": float(recall_score(y_true, predicted_positive, zero_division=0)),
    }


def calibration_bins(y_true: np.ndarray, p: np.ndarray, n_bins: int = 5) -> list[dict]:
    """Reliability curve: mean predicted probability vs. actual positive rate per bin, quantile-
    binned (not fixed-width) so bins stay populated even though most probability mass sits near 0
    at this base rate."""
    if len(p) == 0:
        return []
    edges = np.unique(np.quantile(p, np.linspace(0, 1, n_bins + 1)))
    if len(edges) < 2:
        return [{"bin": 0, "n": len(p), "predicted_mean": float(np.mean(p)), "actual_rate": float(np.mean(y_true))}]

    bin_idx = np.clip(np.digitize(p, edges[1:-1], right=True), 0, len(edges) - 2)
    bins = []
    for b in range(len(edges) - 1):
        mask = bin_idx == b
        if not mask.any():
            continue
        bins.append({
            "bin": b,
            "n": int(mask.sum()),
            "predicted_mean": float(np.mean(p[mask])),
            "actual_rate": float(np.mean(y_true[mask])),
        })
    return bins


def evaluate_binary(y_true: np.ndarray, p: np.ndarray, k_fractions: tuple[float, ...] = (0.02, 0.05, 0.10)) -> dict:
    y_true = np.asarray(y_true)
    p = np.asarray(p)
    base_rate = float(np.mean(y_true)) if len(y_true) else 0.0
    degenerate = base_rate in (0.0, 1.0) or len(y_true) == 0

    return {
        "n_test": len(y_true),
        "base_rate": base_rate,
        "roc_auc": None if degenerate else float(roc_auc_score(y_true, p)),
        "pr_auc": None if degenerate else float(average_precision_score(y_true, p)),
        "precision_recall_at_k": [] if degenerate else [precision_recall_at_k(y_true, p, k) for k in k_fractions],
        "lift_top5pct": None if degenerate or base_rate == 0 else (
            float(np.mean(y_true[np.argsort(-p)[:max(1, int(len(y_true) * 0.05))]])) / base_rate
        ),
        "calibration": calibration_bins(y_true, p),
    }

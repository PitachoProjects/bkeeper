import numpy as np

from app.metrics import calibration_bins, evaluate_binary, precision_recall_at_k, precision_recall_at_threshold


def test_precision_recall_at_threshold_perfect_separation():
    y = np.array([0, 0, 0, 1, 1])
    p = np.array([0.1, 0.2, 0.3, 0.9, 0.95])
    result = precision_recall_at_threshold(y, p, threshold=0.5)
    assert result["precision"] == 1.0
    assert result["recall"] == 1.0
    assert result["n_flagged"] == 2


def test_precision_recall_at_k_flags_exactly_k():
    y = np.array([0, 1, 0, 1, 0, 0, 0, 0, 0, 0])
    p = np.array([0.1, 0.9, 0.2, 0.8, 0.3, 0.4, 0.5, 0.6, 0.05, 0.15])
    result = precision_recall_at_k(y, p, k_fraction=0.2)
    assert result["n_flagged"] == 2
    assert result["precision"] == 1.0  # the two highest-scored rows are the two positives
    assert result["recall"] == 1.0


def test_calibration_bins_reflect_actual_rate_per_bin():
    y = np.array([0] * 50 + [1] * 50)
    p = np.array([0.05] * 50 + [0.95] * 50)
    bins = calibration_bins(y, p, n_bins=2)
    low = next(b for b in bins if b["predicted_mean"] < 0.5)
    high = next(b for b in bins if b["predicted_mean"] >= 0.5)
    assert low["actual_rate"] == 0.0
    assert high["actual_rate"] == 1.0


def test_evaluate_binary_returns_expected_shape():
    y = np.array([0] * 90 + [1] * 10)
    p = np.linspace(0, 1, 100)
    metrics = evaluate_binary(y, p)
    assert metrics["n_test"] == 100
    assert 0 <= metrics["roc_auc"] <= 1
    assert 0 <= metrics["pr_auc"] <= 1
    assert metrics["lift_top5pct"] is not None
    assert len(metrics["precision_recall_at_k"]) == 3
    assert len(metrics["calibration"]) > 0


def test_evaluate_binary_handles_degenerate_all_negative_labels():
    y = np.zeros(20)
    p = np.random.RandomState(0).rand(20)
    metrics = evaluate_binary(y, p)
    assert metrics["roc_auc"] is None
    assert metrics["pr_auc"] is None
    assert metrics["lift_top5pct"] is None

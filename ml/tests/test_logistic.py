from datetime import date, timedelta

import numpy as np

from app import logistic, model_registry, train
from app.features import FEATURE_NAMES
from app.synthetic import generate


def _small_split():
    """A smaller synthetic run than train.py's CLI default, just big enough to have plenty of
    positives in both train and test for calibration and metrics to be meaningful, while keeping
    the test fast."""
    start = date(2024, 1, 1)
    members = generate(n_members=150, weeks=60, seed=1, start=start)
    horizon = start + timedelta(weeks=60)
    rows = train.build_snapshots(members, horizon)
    return train.temporal_split(rows)


def test_logistic_reuses_trains_own_snapshot_builder_and_split():
    # Not a re-implementation: the exact same functions train.py calls.
    assert logistic.build_snapshots is train.build_snapshots
    assert logistic.temporal_split is train.temporal_split


def test_train_and_evaluate_produces_comparable_metrics_and_coefficients():
    train_rows, test_rows = _small_split()
    bundle, metrics = logistic.train_and_evaluate(train_rows, test_rows)

    assert metrics["n_test"] == len(test_rows)
    assert metrics["roc_auc"] is not None
    assert metrics["pr_auc"] is not None
    assert metrics["calibrated"] is True  # this fixture has well over MIN_POSITIVES_TO_CALIBRATE

    coeffs = metrics["coefficients"]
    assert {c["feature"] for c in coeffs} == set(FEATURE_NAMES)
    assert all(c["direction"] in ("increases_risk", "decreases_risk") for c in coeffs)
    # sorted by descending absolute magnitude
    magnitudes = [abs(c["coefficient"]) for c in coeffs]
    assert magnitudes == sorted(magnitudes, reverse=True)

    p = logistic.score(bundle, {f: 0.0 for f in FEATURE_NAMES})
    assert 0.0 <= p <= 1.0


def test_uncalibrated_fallback_when_too_few_positives():
    rng = np.random.RandomState(0)
    x = rng.randn(40, len(FEATURE_NAMES))
    y = np.zeros(40)
    y[:3] = 1  # far below MIN_POSITIVES_TO_CALIBRATE
    proba_model, coef_model = logistic._fit_calibrated(x, y, C=1.0)
    assert proba_model is coef_model


def test_registry_keeps_logistic_and_lightgbm_versions_independent(tmp_path, monkeypatch):
    monkeypatch.setattr(model_registry, "MODELS_DIR", str(tmp_path))

    lgbm_meta = model_registry.save("v1", {"kind": "lgbm-bundle"}, {"auc": 0.8}, FEATURE_NAMES)
    logreg_meta = model_registry.save("v1", {"kind": "logreg-bundle"}, {"auc": 0.75}, FEATURE_NAMES, model_type=logistic.MODEL_TYPE)

    assert lgbm_meta.model_type == model_registry.DEFAULT_MODEL_TYPE
    assert logreg_meta.model_type == "logistic_regression"

    loaded_lgbm = model_registry.load_current()
    loaded_logreg = model_registry.load_current(model_type=logistic.MODEL_TYPE)

    assert loaded_lgbm[0]["kind"] == "lgbm-bundle"
    assert loaded_logreg[0]["kind"] == "logreg-bundle"
    assert loaded_lgbm[1].model_type == model_registry.DEFAULT_MODEL_TYPE
    assert loaded_logreg[1].model_type == "logistic_regression"

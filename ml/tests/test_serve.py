"""
Exercises the route handlers directly (no ASGI test client / extra dependency needed — FastAPI
route functions are plain callables) against a small trained bundle, so it stays fast while still
covering the real request -> feature-build -> score -> response path for both model types.
"""

from datetime import date, timedelta

import pytest

from app import logistic, model_registry, serve, train
from app.synthetic import generate


@pytest.fixture(scope="module")
def small_bundles():
    start = date(2024, 1, 1)
    members = generate(n_members=150, weeks=60, seed=2, start=start)
    horizon = start + timedelta(weeks=60)
    rows = train.build_snapshots(members, horizon)
    train_rows, test_rows = train.temporal_split(rows)

    lgbm_bundle, lgbm_metrics = train.train_and_evaluate(train_rows, test_rows)
    logreg_bundle, logreg_metrics = logistic.train_and_evaluate(train_rows, test_rows)
    return lgbm_bundle, lgbm_metrics, logreg_bundle, logreg_metrics


@pytest.fixture()
def wired_serve(small_bundles, monkeypatch):
    import shap

    lgbm_bundle, lgbm_metrics, logreg_bundle, logreg_metrics = small_bundles
    monkeypatch.setattr(serve, "_model_bundle", lgbm_bundle)
    monkeypatch.setattr(serve, "_model_metadata", model_registry.ModelMetadata("v-test", "2026-01-01T00:00:00", lgbm_bundle["feature_names"], lgbm_metrics))
    monkeypatch.setattr(serve, "_shap_explainer", shap.TreeExplainer(lgbm_bundle["lgbm"]))
    monkeypatch.setattr(serve, "_logistic_bundle", logreg_bundle)
    monkeypatch.setattr(serve, "_logistic_metadata", model_registry.ModelMetadata("v-test", "2026-01-01T00:00:00", logreg_bundle["feature_names"], logreg_metrics, model_type=logistic.MODEL_TYPE))
    return serve


def _sample_request():
    return serve.ScoreRequest(members=[
        serve.MemberScoreRequest(
            member_id="11111111-1111-1111-1111-111111111111",
            join_date=date(2024, 1, 1),
            plan_freq_per_week=3,
            asof=date(2025, 6, 1),
            facts=[
                serve.BookingFactIn(session_date=date(2025, 5, 20), status="attended", window="evening", type_weights={"strength": 1.0}),
            ],
        )
    ])


def test_score_lightgbm_tags_model_type(wired_serve):
    response = wired_serve.score(_sample_request(), x_service_token=wired_serve.SERVICE_TOKEN)
    assert len(response.results) == 1
    result = response.results[0]
    assert result.model_type == model_registry.DEFAULT_MODEL_TYPE
    assert 0.0 <= result.p_churn_28d <= 1.0
    assert result.band in ("red", "amber", "none")


def test_score_logistic_tags_model_type_and_is_independent_of_lightgbm(wired_serve):
    response = wired_serve.score_logistic(_sample_request(), x_service_token=wired_serve.SERVICE_TOKEN)
    assert len(response.results) == 1
    result = response.results[0]
    assert result.model_type == logistic.MODEL_TYPE
    assert result.model_version == "v-test"
    assert 0.0 <= result.p_churn_28d <= 1.0


def test_both_endpoints_reject_bad_token(wired_serve):
    from fastapi import HTTPException

    with pytest.raises(HTTPException):
        wired_serve.score(_sample_request(), x_service_token="wrong")
    with pytest.raises(HTTPException):
        wired_serve.score_logistic(_sample_request(), x_service_token="wrong")


def test_models_endpoint_reports_both_types(wired_serve):
    result = wired_serve.models()
    assert result["lightgbm_ensemble"]["model_type"] == model_registry.DEFAULT_MODEL_TYPE
    assert result["logistic_regression"]["model_type"] == logistic.MODEL_TYPE

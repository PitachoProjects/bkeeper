"""
Scoring service (plan §8: `POST /ml/score`). Loads the current model from the registry at startup;
`BKeeperMlScoringClient` in the .NET Worker calls this weekly in shadow mode (R13).
"""

from __future__ import annotations

import os
from datetime import date, datetime

import numpy as np
import shap
from fastapi import FastAPI, Header, HTTPException
from pydantic import BaseModel

from app import logistic, model_registry
from app.explain import top_reasons, top_reasons_linear
from app.features import BookingFact, FEATURE_NAMES, build_features

SERVICE_TOKEN = os.environ.get("BKEEPER_ML_SERVICE_TOKEN", "dev-ml-token-change-me")
RED_THRESHOLD = float(os.environ.get("BKEEPER_ML_RED_P", "0.08"))
AMBER_THRESHOLD = float(os.environ.get("BKEEPER_ML_AMBER_P", "0.03"))

app = FastAPI(title="BKeeper ML Scoring Service")

_model_bundle = None
_model_metadata = None
_shap_explainer = None
_logistic_bundle = None
_logistic_metadata = None


def _synthetic_train_rows():
    from app.train import build_snapshots, temporal_split
    from app.synthetic import generate
    from datetime import date as _date, timedelta

    train_start = _date(2024, 1, 1)
    members = generate(start=train_start)
    horizon = train_start + timedelta(weeks=104)
    rows = build_snapshots(members, horizon)
    return temporal_split(rows)


@app.on_event("startup")
def load_model():
    global _model_bundle, _model_metadata, _shap_explainer, _logistic_bundle, _logistic_metadata

    loaded = model_registry.load_current()
    if loaded is None:
        # ponytail: auto-train a synthetic-data model on first boot instead of requiring a manual
        # `docker compose run ml python -m app.train` step — good enough until a real export
        # exists, at which point retraining on real data replaces this. See app/train.py.
        from app.train import train_and_evaluate

        train_rows, test_rows = _synthetic_train_rows()
        bundle, metrics = train_and_evaluate(train_rows, test_rows)
        loaded_metadata = model_registry.save("v1-synthetic", bundle, metrics, FEATURE_NAMES)
        loaded = (bundle, loaded_metadata)

    _model_bundle, _model_metadata = loaded
    _shap_explainer = shap.TreeExplainer(_model_bundle["lgbm"])

    # Same self-train-on-first-boot fallback, for the separate Stage B model (see app/logistic.py).
    loaded_logistic = model_registry.load_current(model_type=logistic.MODEL_TYPE)
    if loaded_logistic is None:
        train_rows, test_rows = _synthetic_train_rows()
        bundle, metrics = logistic.train_and_evaluate(train_rows, test_rows)
        loaded_metadata = model_registry.save("v1-synthetic", bundle, metrics, FEATURE_NAMES, model_type=logistic.MODEL_TYPE)
        loaded_logistic = (bundle, loaded_metadata)

    _logistic_bundle, _logistic_metadata = loaded_logistic


class BookingFactIn(BaseModel):
    session_date: date
    status: str
    booked_at: date | None = None
    window: str | None = None
    type_weights: dict[str, float] = {}


class MemberScoreRequest(BaseModel):
    member_id: str
    join_date: date
    plan_freq_per_week: float | None = None
    asof: date
    facts: list[BookingFactIn] = []


class ScoreRequest(BaseModel):
    members: list[MemberScoreRequest]


class MemberScoreResult(BaseModel):
    member_id: str
    p_churn_28d: float
    band: str
    top_reasons: list[str]
    model_version: str
    model_type: str = model_registry.DEFAULT_MODEL_TYPE

    model_config = {"protected_namespaces": ()}  # silence pydantic's "model_" prefix warning


class ScoreResponse(BaseModel):
    scored_at: datetime
    results: list[MemberScoreResult]


def _band(p: float) -> str:
    if p >= RED_THRESHOLD:
        return "red"
    if p >= AMBER_THRESHOLD:
        return "amber"
    return "none"


@app.get("/health")
def health():
    return {
        "status": "ok",
        "model_loaded": _model_bundle is not None,
        "model_version": _model_metadata.version if _model_metadata else None,
        "logistic_model_loaded": _logistic_bundle is not None,
        "logistic_model_version": _logistic_metadata.version if _logistic_metadata else None,
    }


@app.get("/models")
def models():
    """Both registered model types' metadata (version, trained-at, metrics) — Stage C (LightGBM
    ensemble, the shadow-mode default) and Stage B (logistic regression, a comparison view). See
    docs/RUNBOOK.md for how to compare them on a shared backtest."""
    return {
        "lightgbm_ensemble": _model_metadata.__dict__ if _model_metadata else None,
        "logistic_regression": _logistic_metadata.__dict__ if _logistic_metadata else None,
    }


@app.post("/score", response_model=ScoreResponse)
def score(request: ScoreRequest, x_service_token: str = Header(default="")):
    if x_service_token != SERVICE_TOKEN:
        raise HTTPException(status_code=401, detail="Invalid service token.")
    if _model_bundle is None:
        raise HTTPException(status_code=503, detail="No model has been trained yet.")

    scaler, logreg, lgbm = _model_bundle["scaler"], _model_bundle["logreg"], _model_bundle["lgbm"]

    results = []
    for member in request.members:
        facts = [BookingFact(f.session_date, f.status, f.booked_at, f.window, f.type_weights) for f in member.facts]
        feats = build_features(member.join_date, member.plan_freq_per_week, member.asof, facts)
        x = np.array([[feats[f] for f in FEATURE_NAMES]])

        p_logreg = logreg.predict_proba(scaler.transform(x))[0, 1]
        p_lgbm = lgbm.predict_proba(x)[0, 1]
        p_ensemble = float((p_logreg + p_lgbm) / 2)

        shap_values = _shap_explainer.shap_values(x)
        row_shap = shap_values[1][0] if isinstance(shap_values, list) else shap_values[0]
        shap_by_feature = dict(zip(FEATURE_NAMES, row_shap))
        reasons = top_reasons(shap_by_feature, feats)

        results.append(MemberScoreResult(
            member_id=member.member_id,
            p_churn_28d=round(p_ensemble, 4),
            band=_band(p_ensemble),
            top_reasons=reasons,
            model_version=_model_metadata.version,
            model_type=model_registry.DEFAULT_MODEL_TYPE,
        ))

    return ScoreResponse(scored_at=datetime.utcnow(), results=results)


@app.post("/score/logistic", response_model=ScoreResponse)
def score_logistic(request: ScoreRequest, x_service_token: str = Header(default="")):
    """Stage B scoring: the same shadow-mode contract as `/score` (same request shape, same
    member/box eligibility decided by the .NET caller), a different, comparably-evaluated model,
    and reasons from `top_reasons_linear` (coefficient × this member's standardized value) rather
    than SHAP. Never called from a live-alert path — R13 stays wired to the LightGBM ensemble; this
    exists purely for the Manager/Owner comparison view (see RiskScoresController.cs)."""
    if x_service_token != SERVICE_TOKEN:
        raise HTTPException(status_code=401, detail="Invalid service token.")
    if _logistic_bundle is None:
        raise HTTPException(status_code=503, detail="No logistic regression model has been trained yet.")

    results = []
    for member in request.members:
        facts = [BookingFact(f.session_date, f.status, f.booked_at, f.window, f.type_weights) for f in member.facts]
        feats = build_features(member.join_date, member.plan_freq_per_week, member.asof, facts)

        p = logistic.score(_logistic_bundle, feats)
        reasons = top_reasons_linear(_logistic_bundle, feats)

        results.append(MemberScoreResult(
            member_id=member.member_id,
            p_churn_28d=round(p, 4),
            band=_band(p),
            top_reasons=reasons,
            model_version=_logistic_metadata.version,
            model_type=logistic.MODEL_TYPE,
        ))

    return ScoreResponse(scored_at=datetime.utcnow(), results=results)

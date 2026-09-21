"""Minimal file-based model registry: one joblib bundle + a metadata JSON per version."""

from __future__ import annotations

import json
import os
from dataclasses import dataclass, asdict
from datetime import datetime, timezone

import joblib

MODELS_DIR = os.environ.get("BKEEPER_ML_MODELS_DIR", os.path.join(os.path.dirname(__file__), "..", "models"))

# The original (Stage C) model type: kept as the default so every existing call site — train.py,
# serve.py — that doesn't pass model_type keeps writing/reading exactly the file names it always
# has (`{version}.joblib`/`.json`, pointer `current.txt`). A second, distinct model_type (Stage B's
# "logistic_regression") gets its own file prefix and its own pointer file so the two are versioned
# and promoted independently and neither can overwrite the other's registry entries.
DEFAULT_MODEL_TYPE = "lightgbm_ensemble"


@dataclass
class ModelMetadata:
    version: str
    trained_at: str
    feature_names: list[str]
    metrics: dict
    model_type: str = DEFAULT_MODEL_TYPE


def _bundle_path(version: str, model_type: str) -> str:
    stem = version if model_type == DEFAULT_MODEL_TYPE else f"{model_type}-{version}"
    return os.path.join(MODELS_DIR, f"{stem}.joblib")


def _meta_path(version: str, model_type: str) -> str:
    stem = version if model_type == DEFAULT_MODEL_TYPE else f"{model_type}-{version}"
    return os.path.join(MODELS_DIR, f"{stem}.json")


def _current_pointer_path(model_type: str) -> str:
    name = "current.txt" if model_type == DEFAULT_MODEL_TYPE else f"current-{model_type}.txt"
    return os.path.join(MODELS_DIR, name)


def save(version: str, bundle: dict, metrics: dict, feature_names: list[str], model_type: str = DEFAULT_MODEL_TYPE) -> ModelMetadata:
    os.makedirs(MODELS_DIR, exist_ok=True)
    joblib.dump(bundle, _bundle_path(version, model_type))

    metadata = ModelMetadata(
        version=version,
        trained_at=datetime.now(timezone.utc).isoformat(),
        feature_names=feature_names,
        metrics=metrics,
        model_type=model_type,
    )
    with open(_meta_path(version, model_type), "w") as f:
        json.dump(asdict(metadata), f, indent=2)

    with open(_current_pointer_path(model_type), "w") as f:
        f.write(version)

    return metadata


def load_current(model_type: str = DEFAULT_MODEL_TYPE) -> tuple[dict, ModelMetadata] | None:
    current_path = _current_pointer_path(model_type)
    if not os.path.exists(current_path):
        return None
    with open(current_path) as f:
        version = f.read().strip()

    bundle_path = _bundle_path(version, model_type)
    meta_path = _meta_path(version, model_type)
    if not os.path.exists(bundle_path) or not os.path.exists(meta_path):
        return None

    bundle = joblib.load(bundle_path)
    with open(meta_path) as f:
        raw = json.load(f)
    raw.setdefault("model_type", DEFAULT_MODEL_TYPE)  # metadata saved before model_type existed
    return bundle, ModelMetadata(**raw)

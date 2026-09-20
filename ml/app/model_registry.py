"""Minimal file-based model registry: one joblib bundle + a metadata JSON per version."""

from __future__ import annotations

import json
import os
from dataclasses import dataclass, asdict
from datetime import datetime, timezone

import joblib

MODELS_DIR = os.environ.get("BKEEPER_ML_MODELS_DIR", os.path.join(os.path.dirname(__file__), "..", "models"))


@dataclass
class ModelMetadata:
    version: str
    trained_at: str
    feature_names: list[str]
    metrics: dict


def save(version: str, bundle: dict, metrics: dict, feature_names: list[str]) -> ModelMetadata:
    os.makedirs(MODELS_DIR, exist_ok=True)
    joblib.dump(bundle, os.path.join(MODELS_DIR, f"{version}.joblib"))

    metadata = ModelMetadata(version=version, trained_at=datetime.now(timezone.utc).isoformat(), feature_names=feature_names, metrics=metrics)
    with open(os.path.join(MODELS_DIR, f"{version}.json"), "w") as f:
        json.dump(asdict(metadata), f, indent=2)

    with open(os.path.join(MODELS_DIR, "current.txt"), "w") as f:
        f.write(version)

    return metadata


def load_current() -> tuple[dict, ModelMetadata] | None:
    current_path = os.path.join(MODELS_DIR, "current.txt")
    if not os.path.exists(current_path):
        return None
    with open(current_path) as f:
        version = f.read().strip()

    bundle_path = os.path.join(MODELS_DIR, f"{version}.joblib")
    meta_path = os.path.join(MODELS_DIR, f"{version}.json")
    if not os.path.exists(bundle_path) or not os.path.exists(meta_path):
        return None

    bundle = joblib.load(bundle_path)
    with open(meta_path) as f:
        raw = json.load(f)
    return bundle, ModelMetadata(**raw)

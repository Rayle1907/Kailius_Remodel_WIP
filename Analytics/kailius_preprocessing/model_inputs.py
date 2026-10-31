import hashlib
import json
from pathlib import Path
from typing import Any, Mapping, Sequence

import pandas as pd
from sklearn.compose import ColumnTransformer
from sklearn.impute import SimpleImputer
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import OneHotEncoder, StandardScaler

from .geometry import load_catalogue

NUMERIC = [
    "health", "defense", "attack_damage", "move_speed", "jump_height", "gravity",
    "defeated_required_enemies", "total_required_enemies", "wall_distance", "wall_active",
    "attempt_number", "prior_observed_attempts", "prior_observed_failures",
    "prior_segment_observed_failures", "prior_active_seconds", "history_incomplete",
]
CATEGORICAL = ["input_method", "platform", "offer_variant", "start_reason", "previous_outcome"]


def split_players(rows: Sequence[Mapping[str, Any]], seed: int) -> dict[str, str]:
    partitions = {}
    player_ids = sorted({row["player_id"] for row in rows})
    for player_id in player_ids:
        bucket = int(hashlib.sha256(f"{seed}:{player_id}".encode()).hexdigest()[:12], 16) / 16 ** 12
        partitions[player_id] = "train" if bucket < .6 else "validation" if bucket < .8 else "test"
    return partitions


def preprocessing(scale: bool, numeric_features: Sequence[str]) -> ColumnTransformer:
    numeric_steps = [
        ("impute", SimpleImputer(strategy="median", add_indicator=True, keep_empty_features=True))
    ]
    if scale:
        numeric_steps.append(("scale", StandardScaler()))

    categorical_steps = Pipeline([
        ("impute", SimpleImputer(strategy="constant", fill_value="unknown")),
        ("encode", OneHotEncoder(handle_unknown="ignore", sparse_output=False)),
    ])
    numeric_pipeline = Pipeline(numeric_steps)
    return ColumnTransformer([
        ("numeric", numeric_pipeline, list(numeric_features)),
        ("categorical", categorical_steps, CATEGORICAL),
    ], remainder="drop", verbose_feature_names_out=True)


def _training_cohort(dataset: Path) -> pd.DataFrame:
    model_rows = pd.read_parquet(dataset / "model_rows.parquet")
    cohort = model_rows.loc[(model_rows["split"] == "train") & model_rows["first_attempt"]].copy()
    cohort = cohort.sort_values(
        ["player_id", "segment_id", "segment_version", "attempt_id"], kind="mergesort"
    )
    return cohort.drop_duplicates("player_id", keep="first")


def _out_of_range_features(
    features: Mapping[str, float], train_ranges: Mapping[str, Mapping[str, float]]
) -> list[str]:
    return [
        name
        for name, value in features.items()
        if name not in train_ranges
        or not train_ranges[name]["min"] <= value <= train_ranges[name]["max"]
    ]


def _candidate_record(
    identity: tuple[str, str, str],
    features: Mapping[str, float],
    out_of_range: list[str],
    player_row: Mapping[str, Any],
) -> dict[str, Any]:
    candidate = {name: player_row.get(name) for name in NUMERIC + CATEGORICAL}
    candidate.update(
        player_id=player_row["player_id"],
        source_attempt_id=player_row["attempt_id"],
        source_segment_id=player_row["segment_id"],
        source_segment_version=player_row["segment_version"],
        **features,
    )
    candidate.update(
        candidate_segment_id=identity[0],
        candidate_segment_version=identity[1],
        candidate_geometry_hash=identity[2],
        out_of_range_features=",".join(out_of_range),
    )
    return candidate


def candidate_rows(catalogue_path: Path, dataset: Path, output: Path) -> None:
    dataset, output = Path(dataset), Path(output)
    contract = json.loads((dataset / "feature_contract.json").read_text())
    cohort = _training_cohort(dataset)
    if cohort.empty:
        raise ValueError("No frozen training first-attempt cohort")

    candidates = load_catalogue([catalogue_path])
    train_ranges = contract["train_ranges"]
    results = []
    for identity, segment in sorted(candidates.items()):
        features = segment["derived_geometry_features"]
        out_of_range = _out_of_range_features(features, train_ranges)
        results.extend(
            _candidate_record(identity, features, out_of_range, player_row)
            for player_row in cohort.to_dict("records")
        )

    output.parent.mkdir(parents=True, exist_ok=True)
    if output.exists():
        raise ValueError("Candidate output already exists")
    pd.DataFrame(results).to_parquet(output, index=False)

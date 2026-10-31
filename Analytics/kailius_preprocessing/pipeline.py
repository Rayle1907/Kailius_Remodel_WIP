import glob
import hashlib
import importlib.metadata
import json
import os
import platform
import shutil
import tempfile
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Sequence

import joblib
import pandas as pd

from . import __version__
from .attempts import Attempt, add_history, reconstruct
from .events import END, START, Event, normalize
from .geometry import load_catalogue as catalogue
from .model_inputs import CATEGORICAL, NUMERIC, candidate_rows, preprocessing, split_players
from .reports import difficulty_report, revive_journeys

Config = dict[str, Any]
Record = dict[str, Any]

EMPTY_SCHEMAS = {
    "events": ["event_id", "event_name", "event_timestamp", "environment"],
    "quarantine": ["source", "row", "reason", "attempt_id", "event_id"],
    "attempts": ["attempt_id", "player_id", "segment_id", "segment_version", "geometry_hash", "outcome", "label", "split"],
    "revive_journeys": ["offer_id", "attempt_id", "response", "post_decision_outcome", "quality"],
    "difficulty": ["segment_id", "segment_version", "geometry_hash", "resolved_first_attempts", "status"],
    "splits": ["player_id", "split"],
}


def digest(path: Path) -> str:
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def dump(path: Path, value: object) -> None:
    Path(path).write_text(json.dumps(value, indent=2, allow_nan=False), encoding="utf-8")


def write_table(output: Path, name: str, records: Sequence[Record]) -> None:
    frame = pd.DataFrame(records)
    if frame.empty:
        frame = pd.DataFrame(columns=EMPTY_SCHEMAS.get(name, []))
    frame.to_parquet(output / (name + ".parquet"), index=False)
    frame.to_csv(output / (name + ".csv"), index=False)


def _model_feature_columns(segments: dict[tuple[str, str, str], Record]) -> tuple[list[str], list[str]]:
    geometry_columns = sorted(set().union(*(
        segment["derived_geometry_features"] for segment in segments.values()
    )))
    numeric_features = NUMERIC + geometry_columns
    return numeric_features, numeric_features + CATEGORICAL


def _labelled_model_rows(rows: Sequence[Attempt], feature_columns: Sequence[str]) -> tuple[pd.DataFrame, list[Attempt]]:
    labelled_attempts = [row for row in rows if row["label"] is not None]
    retained_columns = list(dict.fromkeys(
        list(feature_columns) + [
            "label", "split", "player_id", "attempt_id", "first_attempt",
            "segment_id", "segment_version", "geometry_hash",
        ]
    ))
    model_rows = pd.DataFrame(labelled_attempts).reindex(columns=retained_columns)
    for column in CATEGORICAL:
        model_rows[column] = model_rows[column].fillna("unknown").astype(str)
    return model_rows, labelled_attempts


def _missing_class_partitions(model_rows: pd.DataFrame) -> list[str]:
    return [
        partition
        for partition in ("train", "validation", "test")
        if set(model_rows.loc[model_rows["split"] == partition, "label"]) != {0, 1}
    ]


def _write_model_transformers(
    output: Path,
    model_rows: pd.DataFrame,
    feature_columns: Sequence[str],
    numeric_features: Sequence[str],
) -> None:
    training_features = model_rows.loc[model_rows["split"] == "train", feature_columns]
    for model_name, scale in (("logistic_regression", True), ("random_forest", False)):
        transformer = preprocessing(scale, numeric_features)
        transformer.fit(training_features)
        joblib.dump(transformer, output / (model_name + "_preprocessor.joblib"))
        feature_names = transformer.get_feature_names_out().tolist()
        dump(output / (model_name + "_feature_names.json"), feature_names)

        for partition in ("train", "validation", "test"):
            partition_rows = model_rows.loc[model_rows["split"] == partition]
            transformed = transformer.transform(partition_rows[feature_columns])
            pd.DataFrame(transformed, columns=feature_names).to_parquet(
                output / (model_name + "_" + partition + "_X.parquet"), index=False
            )
            partition_rows[["attempt_id", "player_id", "label", "first_attempt"]].to_parquet(
                output / (model_name + "_" + partition + "_y.parquet"), index=False
            )


def _training_ranges(
    model_rows: pd.DataFrame, numeric_features: Sequence[str]
) -> dict[str, dict[str, float]]:
    training_rows = model_rows.loc[model_rows["split"] == "train"]
    return {
        feature: {"min": float(training_rows[feature].min()), "max": float(training_rows[feature].max())}
        for feature in numeric_features
        if training_rows[feature].notna().any()
    }


def write_model_datasets(
    output: Path,
    rows: Sequence[Attempt],
    segments: dict[tuple[str, str, str], Record],
) -> dict[str, Any]:
    numeric_features, feature_columns = _model_feature_columns(segments)
    model_rows, labelled_attempts = _labelled_model_rows(rows, feature_columns)
    model_rows.to_parquet(output / "model_rows.parquet", index=False)

    missing_classes = _missing_class_partitions(model_rows)
    status = "insufficient_data" if missing_classes else "ready"
    if not missing_classes:
        _write_model_transformers(output, model_rows, feature_columns, numeric_features)

    dump(output / "feature_contract.json", {
        "numeric": numeric_features,
        "categorical": CATEGORICAL,
        "train_ranges": _training_ranges(model_rows, numeric_features),
    })
    return {
        "status": status,
        "missing_classes": missing_classes,
        "model_rows": labelled_attempts,
        "numeric_features": numeric_features,
    }


def write_manifest(
    output: Path, config: Config, config_path: Path, source_paths: Sequence[Path]
) -> None:
    query_file = config.get("query_file", "export.sql")
    query_path = config_path.parent / query_file if query_file else None
    manifest = {
        "pipeline_version": __version__,
        "config": config,
        "config_sha256": digest(config_path),
        "source_code_sha256": {
            path.name: digest(path) for path in Path(__file__).parent.glob("*.py")
        },
        "source_files": [{"path": str(path), "sha256": digest(path)} for path in source_paths],
        "query_sha256": digest(query_path) if query_path is not None and query_path.exists() else None,
        "created_utc": datetime.now(timezone.utc).isoformat(),
        "extraction_cutoff_utc": config.get("extraction_cutoff_utc"),
        "python": platform.python_version(),
        "packages": {
            name: importlib.metadata.version(name)
            for name in ("pandas", "pyarrow", "scikit-learn", "joblib")
        },
        "seed": config.get("seed", 42),
    }
    dump(output / "manifest.json", manifest)


def build_quality_report(
    counts: dict[str, int],
    events: Sequence[Event],
    rows: Sequence[Attempt],
    model_rows: Sequence[Attempt],
    quarantine: Sequence[Record],
    splits: dict[str, str],
    model_status: str,
    missing_classes: Sequence[str],
) -> dict[str, Any]:
    report = counts | {
        "status": model_status,
        "valid_events": len(events),
        "attempts": len(rows),
        "labelled_attempts": len(model_rows),
        "unresolved_attempts": sum(row["label"] is None for row in rows),
        "rejections_by_reason": dict(Counter(item["reason"] for item in quarantine)),
        "partitions_missing_both_classes": list(missing_classes),
        "players_per_split": dict(Counter(splits.values())),
    }
    report["unresolved_by_segment_and_variant"] = dict(Counter(
        row["segment_id"] + ":" + row["segment_version"] + ":" + str(row.get("offer_variant", "unknown"))
        for row in rows
        if row["label"] is None
    ))
    return report


def _reconstruct_attempts(
    events: Sequence[Event], quarantine: list[Record], segments: dict[tuple[str, str, str], Record]
) -> list[Attempt]:
    contaminated_attempts = {
        item["attempt_id"] for item in quarantine if item.get("attempt_id")
    }
    eligible_events = [
        event for event in events
        if event.get("attempt_id") not in contaminated_attempts
    ]
    rows, attempt_rejections = reconstruct(eligible_events, segments)
    quarantine.extend(attempt_rejections)
    return rows


def _write_dataset_tables(
    output: Path,
    events: list[Event],
    quarantine: list[Record],
    rows: list[Attempt],
    segments: dict[tuple[str, str, str], Record],
    splits: dict[str, str],
    minimum_players: int,
) -> None:
    write_table(output, "events", events)
    write_table(output, "quarantine", quarantine)
    write_table(output, "attempts", rows)
    write_table(output, "revive_journeys", revive_journeys(events))
    write_table(output, "difficulty", difficulty_report(rows, segments, minimum_players))
    write_table(output, "splits", [
        {"player_id": player_id, "split": split}
        for player_id, split in splits.items()
    ])


def _build_dataset(
    config: Config,
    config_path: Path,
    input_paths: list[Path],
    catalogue_paths: list[Path],
    output: Path,
) -> dict[str, Any]:
    segments = catalogue(catalogue_paths)
    events, quarantine, counts = normalize(
        input_paths,
        config.get("environment", "production"),
        config.get("expected_raw_rows"),
    )
    rows = _reconstruct_attempts(events, quarantine, segments)
    splits = split_players(rows, config.get("seed", 42))
    for row in rows:
        row["split"] = splits[row["player_id"]]

    _write_dataset_tables(
        output, events, quarantine, rows, segments, splits, config.get("minimum_players", 30)
    )
    model_results = write_model_datasets(output, rows, segments)
    write_manifest(output, config, config_path, input_paths + catalogue_paths)
    report = build_quality_report(
        counts,
        events,
        rows,
        model_results["model_rows"],
        quarantine,
        splits,
        model_results["status"],
        model_results["missing_classes"],
    )
    dump(output / "quality_report.json", report)
    return report


def _validate_config(config: Any) -> Config:
    if not isinstance(config, dict):
        raise ValueError("Configuration must be a JSON object")
    required = ("input_globs", "catalogue_globs")
    missing = [field for field in required if field not in config]
    if missing:
        raise ValueError("Missing configuration field(s): " + ", ".join(missing))
    if not isinstance(config["input_globs"], list) or not isinstance(config["catalogue_globs"], list):
        raise ValueError("input_globs and catalogue_globs must be lists")
    if any(
        not isinstance(pattern, str) or not pattern
        for pattern in config["input_globs"] + config["catalogue_globs"]
    ):
        raise ValueError("Input and catalogue glob patterns must be non-empty strings")

    output_dir = config.get("output_dir", "processed")
    if not isinstance(output_dir, str) or not output_dir or Path(output_dir).is_absolute():
        raise ValueError("output_dir must be a non-empty relative path")
    if config.get("environment", "production") not in ("production", "development", "test"):
        raise ValueError("environment must be production, development, or test")
    if config.get("extraction_cutoff_utc") is not None and not isinstance(config["extraction_cutoff_utc"], str):
        raise ValueError("extraction_cutoff_utc must be a string or null")

    query_file = config.get("query_file", "export.sql")
    if query_file is not None and (not isinstance(query_file, str) or not query_file):
        raise ValueError("query_file must be a non-empty string or null")
    expected_raw_rows = config.get("expected_raw_rows")
    if expected_raw_rows is not None and (
        type(expected_raw_rows) is not int or expected_raw_rows < 0
    ):
        raise ValueError("expected_raw_rows must be a non-negative integer or null")
    minimum_players = config.get("minimum_players", 30)
    if type(minimum_players) is not int or minimum_players < 1:
        raise ValueError("minimum_players must be a positive integer")
    if type(config.get("seed", 42)) is not int:
        raise ValueError("seed must be an integer")
    return config


def _read_config(config_path: Path) -> Config:
    config_data = json.loads(config_path.read_text(encoding="utf-8-sig"))
    return _validate_config(config_data)


def _discover_inputs(config_path: Path, config: Config) -> tuple[list[Path], list[Path]]:
    root = config_path.parent
    input_paths = sorted({
        Path(path)
        for pattern in config["input_globs"]
        for path in glob.glob(str(root / pattern))
    })
    catalogue_paths = sorted({
        Path(path)
        for pattern in config["catalogue_globs"]
        for path in glob.glob(str(root / pattern))
    })
    return input_paths, catalogue_paths


def _validate_inputs_and_destination(
    input_paths: Sequence[Path], catalogue_paths: Sequence[Path], destination: Path
) -> None:
    if not input_paths or not catalogue_paths:
        raise ValueError("CSV exports and an authored segment catalogue are required")
    if any(
        path.resolve() == destination or destination in path.resolve().parents
        for path in (*input_paths, *catalogue_paths)
    ):
        raise ValueError("Outputs must be separate from source inputs")
    if destination.exists() and any(destination.iterdir()):
        raise ValueError("Use a new empty output directory for each reproducible dataset")


def _publish_dataset(
    config: Config,
    config_path: Path,
    input_paths: list[Path],
    catalogue_paths: list[Path],
    destination: Path,
) -> dict[str, Any]:
    destination.parent.mkdir(parents=True, exist_ok=True)
    temporary = Path(tempfile.mkdtemp(prefix=destination.name + ".tmp-", dir=destination.parent))
    try:
        report = _build_dataset(config, config_path, input_paths, catalogue_paths, temporary)
        if destination.exists():
            destination.rmdir()
        os.replace(temporary, destination)
        return report
    except Exception:
        shutil.rmtree(temporary, ignore_errors=True)
        raise


def build(config_path: Path) -> dict[str, Any]:
    config_path = Path(config_path).resolve()
    config = _read_config(config_path)
    input_paths, catalogue_paths = _discover_inputs(config_path, config)
    destination = (config_path.parent / config.get("output_dir", "processed")).resolve()
    _validate_inputs_and_destination(input_paths, catalogue_paths, destination)
    return _publish_dataset(config, config_path, input_paths, catalogue_paths, destination)

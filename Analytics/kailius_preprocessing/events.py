import json
import math
from collections import defaultdict
from pathlib import Path
from typing import Any, Sequence

import pandas as pd
from .schema_contract import EVENT_CLASSES

START = "segment_attempt_started"
END = "segment_attempt_ended"
RAW_NUMERIC = [
    "health", "defense", "attack_damage", "move_speed", "jump_height", "gravity",
    "defeated_required_enemies", "total_required_enemies", "wall_distance", "wall_active",
]
INTEGER_PARAMETERS = {
    "schema_version", "event_sequence", "attempt_number", "health", "defense", "attack_damage",
    "defeated_required_enemies", "total_required_enemies", "death_number_total", "death_number_in_segment",
    "death_number_in_gauntlet_session", "player_health_before_death", "player_health_before_failure",
    "player_health_after_failure", "premium_currency_balance", "revive_price_int", "offer_number_in_gauntlet",
    "balance_after_decision", "offers_shown_in_gauntlet", "next_revive_price_int", "exit_health", "run_deaths",
    "accepted_revives", "survey_answered_count", "survey_pxi_answered_count",
    "pxi_autonomy_1", "pxi_autonomy_2", "pxi_autonomy_3", "pxi_mastery_1", "pxi_mastery_2", "pxi_mastery_3",
    "pxi_challenge_1", "pxi_challenge_2", "pxi_challenge_3", "pxi_progress_feedback_1",
    "pxi_progress_feedback_2", "pxi_progress_feedback_3", "background_experience_skill_1",
    "background_experience_skill_2", "background_experience_skill_3",
}
FLOAT_PARAMETERS = {
    "duration_seconds", "progress_x", "progress_y", "time_since_session_start",
    "time_since_last_death", "time_since_last_failure", "time_to_decision", "seconds_after_decision",
    "run_duration_seconds", "move_speed", "jump_height", "gravity", "wall_distance",
}
BOOLEAN_PARAMETERS = {"wall_active", "offer_eligible", "offer_shown", "can_afford", "survey_complete"}
ALIASES = {
    "EVENT_ID": "event_id", "EVENT_NAME": "event_name", "EVENT_TIMESTAMP": "event_timestamp",
    "UNITY_ENVIRONMENT_NAME": "environment", "LOADED_TIMESTAMP": "loaded_timestamp",
    "USER_ID": "sdk_user_id", "SESSION_ID": "sdk_session_id", "ENVIRONMENT_NAME": "environment",
    "PLATFORM": "platform", "CLIENT_VERSION": "client_version", "eventUUID": "event_id",
    "eventName": "event_name", "eventTimestamp": "event_timestamp", "userID": "sdk_user_id",
    "sessionID": "sdk_session_id", "clientVersion": "client_version",
}

Event = dict[str, Any]
Rejection = dict[str, Any]


def timestamp(value: Any) -> str:
    parsed = pd.to_datetime(value, utc=True, errors="raise")
    if pd.isna(parsed):
        raise ValueError("Missing timestamp")
    return parsed.isoformat()


def number(value: Any, integer: bool = False) -> int | float:
    result = float(value)
    if not math.isfinite(result) or (integer and not result.is_integer()):
        raise ValueError("Invalid number")
    return int(result) if integer else result


def sql_event(raw: dict[str, Any]) -> Event:
    payload_text = raw.get("EVENT_JSON", raw.get("event_json", ""))
    payload = json.loads(payload_text) if payload_text else {}
    if not isinstance(payload, dict):
        raise ValueError("Event JSON must be an object")
    parameters = payload.get("eventParams", payload)
    if not isinstance(parameters, dict):
        raise ValueError("Event parameters must be an object")
    event = {
        ALIASES.get(key, key.lower()): value
        for key, value in payload.items()
        if key != "eventParams"
    }
    event.update({ALIASES.get(key, key): value for key, value in parameters.items()})
    event.update({
        ALIASES.get(key, key.lower()): value
        for key, value in raw.items()
        if value != "" and key.lower() != "event_json"
    })
    return event


def flat_event(raw: dict[str, Any]) -> Event:
    return {
        ALIASES.get(key, key.lower()): value
        for key, value in raw.items()
        if value != ""
    }


def _normalize_event_parameters(row: Event) -> None:
    for field in INTEGER_PARAMETERS:
        if field in row and row[field] not in (None, ""):
            row[field] = number(row[field], True)
    for field in FLOAT_PARAMETERS:
        if field in row and row[field] not in (None, ""):
            row[field] = number(row[field])
    for field in BOOLEAN_PARAMETERS:
        if field not in row or row[field] in (None, ""):
            continue
        value = row[field]
        if isinstance(value, bool):
            continue
        lowered = str(value).lower()
        if lowered not in ("true", "false"):
            raise ValueError("Invalid boolean " + field)
        row[field] = lowered == "true"


def _normalize_attempt_fields(row: Event) -> None:
    required_fields = (
        "attempt_id", "playthrough_id", "run_id",
        "gauntlet_session_id", "offer_variant", "experiment_version",
        "level_segment_id", "segment_version", "geometry_hash"
    )
    for field in required_fields:
        if not row.get(field):
            raise ValueError("Missing " + field)
    if row.get("attempt_number") not in (None, ""):
        row["attempt_number"] = number(row["attempt_number"], True)

    for field in RAW_NUMERIC:
        if field not in row or row[field] in (None, ""):
            continue
        value = row[field]
        if field == "wall_active" and str(value).lower() in ("true", "false"):
            value = int(str(value).lower() == "true")
        row[field] = number(value)
        if row[field] == -1 and field != "gravity":
            row[field] = None
        elif row[field] < 0 and field != "gravity":
            raise ValueError("Negative " + field)

    if row["event_name"] == START:
        row["attempt_number"] = number(row.get("attempt_number"), True)
        if row["attempt_number"] < 1:
            raise ValueError("Invalid exposure counter")
        for field in ("defeated_required_enemies", "total_required_enemies"):
            if field in row and row[field] is not None and not float(row[field]).is_integer():
                raise ValueError("Invalid integer " + field)
        return

    if row.get("outcome") not in ("success", "failure", "unresolved"):
        raise ValueError("Unknown outcome")
    row["duration_seconds"] = number(row.get("duration_seconds"))
    if row["duration_seconds"] < 0:
        raise ValueError("Negative duration")


def _normalize_research_identity(row: Event) -> None:
    if row.get("schema_version") != 2:
        raise ValueError("Unsupported event schema")
    if row.get("event_sequence") in (None, ""):
        raise ValueError("Missing event_sequence")
    if row["event_sequence"] < 1:
        raise ValueError("Invalid session order")
    if row.get("time_since_session_start") in (None, ""):
        raise ValueError("Missing time_since_session_start")
    if row["time_since_session_start"] < 0:
        raise ValueError("Invalid session order")


def _normalize_metadata(row: Event) -> None:
    row["player_id"] = row.get("sdk_user_id") or None
    row["application_session_id"] = row.get("sdk_session_id") or None
    row["build_version"] = row.get("client_version") or None
    row.pop("elapsed_seconds", None)


def _normalize_row(
    raw: dict[str, Any], path: Path, source_row: int, environment: str
) -> tuple[Event | None, Rejection | None]:
    row: Event = {}
    try:
        has_sql_columns = any(key in raw for key in ("EVENT_ID", "EVENT_NAME", "EVENT_TIMESTAMP"))
        row = sql_event(raw) if has_sql_columns else flat_event(raw)
        for field in ("event_id", "event_name", "event_timestamp", "environment"):
            if not row.get(field):
                raise ValueError("Missing " + field)
        if row["environment"] != environment:
            return None, {"source": str(path), "row": source_row, "reason": "environment_filtered"}

        row["event_timestamp"] = timestamp(row["event_timestamp"])
        if row.get("loaded_timestamp"):
            row["loaded_timestamp"] = timestamp(row["loaded_timestamp"])
        _normalize_event_parameters(row)
        _normalize_metadata(row)
        if row["event_name"] in EVENT_CLASSES:
            _normalize_research_identity(row)
        else:
            if row.get("event_sequence") not in (None, ""):
                row["event_sequence"] = number(row["event_sequence"], True)
        if row["event_name"] in (START, END):
            _normalize_attempt_fields(row)

        row["source_file"] = str(path)
        row["source_row"] = source_row
        return row, None
    except (ValueError, TypeError, KeyError) as error:
        return None, {
            "source": str(path), "row": source_row, "reason": str(error),
            "attempt_id": row.get("attempt_id"),
        }


def _event_version(event: Event) -> str:
    comparable = {
        key: value
        for key, value in event.items()
        if key not in ("source_file", "source_row", "loaded_timestamp")
    }
    return json.dumps(comparable, sort_keys=True)


def _deduplicate_events(events: Sequence[Event]) -> tuple[list[Event], list[Rejection], int]:
    events_by_id: dict[str, list[Event]] = defaultdict(list)
    for event in events:
        events_by_id[event["event_id"]].append(event)

    clean_events = []
    rejected = []
    duplicate_count = 0
    for event_id, copies in events_by_id.items():
        versions = {_event_version(event) for event in copies}
        if len(versions) != 1:
            rejected.extend({
                "event_id": event_id,
                "attempt_id": event.get("attempt_id"),
                "reason": "conflicting_event_uuid",
            } for event in copies)
            continue
        clean_events.append(copies[0])
        duplicate_count += len(copies) - 1
    return clean_events, rejected, duplicate_count


def _remove_conflicting_session_sequences(
    events: Sequence[Event],
) -> tuple[list[Event], list[Rejection]]:
    sequences: dict[tuple[Any, Any, Any], list[Event]] = defaultdict(list)
    for event in events:
        if event.get("event_sequence") not in (None, "") and event.get("player_id") and event.get("application_session_id"):
            key = (event["player_id"], event["application_session_id"], event["event_sequence"])
            sequences[key].append(event)

    conflicting_ids = set()
    rejected = []
    for group in sequences.values():
        if len(group) <= 1:
            continue
        for event in group:
            conflicting_ids.add(event["event_id"])
            rejected.append({
                "event_id": event["event_id"],
                "attempt_id": event.get("attempt_id"),
                "reason": "conflicting_session_sequence",
            })
    return [event for event in events if event["event_id"] not in conflicting_ids], rejected


def normalize(
    paths: Sequence[Path], environment: str, expected: int | None = None
) -> tuple[list[Event], list[Rejection], dict[str, Any]]:
    raw_count = 0
    events = []
    rejected = []
    for path in paths:
        source = pd.read_csv(path, dtype=str, keep_default_na=False)
        raw_count += len(source)
        for source_row, raw in enumerate(source.to_dict("records"), 2):
            event, rejection = _normalize_row(raw, path, source_row, environment)
            if rejection is not None:
                rejected.append(rejection)
            if event is not None:
                events.append(event)

    if expected is not None and raw_count != expected:
        raise ValueError(f"Export count mismatch: expected {expected}, received {raw_count}")

    events, duplicate_rejections, duplicate_count = _deduplicate_events(events)
    rejected.extend(duplicate_rejections)
    events, sequence_rejections = _remove_conflicting_session_sequences(events)
    rejected.extend(sequence_rejections)
    events.sort(key=lambda event: (
        event.get("application_session_id") or "", event.get("event_sequence") or 0,
        event["event_timestamp"], event["event_id"],
    ))
    rejected.sort(key=lambda item: (
        item.get("event_id", ""), item.get("attempt_id", ""), item.get("source", ""),
        item.get("row", 0), item["reason"],
    ))
    return events, rejected, {"raw_rows": raw_count, "deduplicated_copies": duplicate_count}

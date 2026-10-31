from collections import defaultdict
from typing import Any, Mapping, Sequence

from .events import END, START
from .geometry import geometry_features

Event = dict[str, Any]
Attempt = dict[str, Any]
SegmentCatalog = Mapping[tuple[str, str, str], Mapping[str, Any]]


def _group_attempt_events(events: Sequence[Event]) -> dict[str, list[Event]]:
    grouped: dict[str, list[Event]] = defaultdict(list)
    for event in events:
        if event["event_name"] in (START, END):
            grouped[event["attempt_id"]].append(event)
    return grouped


def _attempt_boundary_error(starts: Sequence[Event], ends: Sequence[Event]) -> str | None:
    if len(starts) != 1 or len(ends) > 1:
        return "missing_or_conflicting_attempt_boundaries"

    if ends and any(
        starts[0].get(field) != ends[0].get(field)
        for field in ("player_id", "application_session_id", "segment_id", "segment_version", "geometry_hash")
    ):
        return "terminal_identity_mismatch"
    if ends and (
        ends[0]["event_sequence"] <= starts[0]["event_sequence"]
        or ends[0]["elapsed_seconds"] < starts[0]["elapsed_seconds"]
    ):
        return "terminal_precedes_start"
    if ends and ends[0]["duration_seconds"] > (
        ends[0]["elapsed_seconds"] - starts[0]["elapsed_seconds"] + 1
    ):
        return "active_duration_exceeds_observed_time"
    return None


def _attempt_row(start: Event, end: Event, segments: SegmentCatalog) -> Attempt | None:
    identity = tuple(start[field] for field in ("segment_id", "segment_version", "geometry_hash"))
    if identity not in segments:
        return None

    segment = segments[identity]
    outcome = end.get("outcome", "unresolved")
    row = dict(start)
    row.update(segment.get("derived_geometry_features") or geometry_features(segment))
    row.update(
        outcome=outcome,
        label={"failure": 1, "success": 0}.get(outcome),
        outcome_reason=end.get("reason", "missing_terminal_event"),
        duration_seconds=end.get("duration_seconds"),
        end_elapsed_seconds=end.get("elapsed_seconds"),
        end_sequence=end.get("event_sequence"),
        first_attempt=start["attempt_number"] == 1,
    )
    return row


def _overlapping_attempt_ids(rows: Sequence[Attempt]) -> set[str]:
    sessions: dict[tuple[str, str], list[Attempt]] = defaultdict(list)
    for row in rows:
        sessions[(row["player_id"], row["application_session_id"])].append(row)

    overlapping = set()
    for session_rows in sessions.values():
        session_rows.sort(key=lambda row: row["event_sequence"])
        active = []
        for current in session_rows:
            active = [
                prior for prior in active
                if prior["end_sequence"] is not None
                and prior["end_sequence"] >= current["event_sequence"]
            ]
            if active:
                overlapping.add(current["attempt_id"])
                overlapping.update(prior["attempt_id"] for prior in active)
            if current["end_sequence"] is not None:
                active.append(current)
    return overlapping


def reconstruct(
    events: Sequence[Event], segments: SegmentCatalog
) -> tuple[list[Attempt], list[dict[str, str]]]:
    rows = []
    rejected: list[dict[str, str]] = []
    for attempt_id, records in sorted(_group_attempt_events(events).items()):
        starts = [event for event in records if event["event_name"] == START]
        ends = [event for event in records if event["event_name"] == END]
        reason = _attempt_boundary_error(starts, ends)
        if reason:
            rejected.append({"attempt_id": attempt_id, "reason": reason})
            continue

        start = starts[0]
        row = _attempt_row(start, ends[0] if ends else {}, segments)
        if row is None:
            rejected.append({"attempt_id": attempt_id, "reason": "unknown_geometry_version"})
            continue
        rows.append(row)

    overlapping = _overlapping_attempt_ids(rows)
    rejected.extend(
        {"attempt_id": attempt_id, "reason": "overlapping_attempts"}
        for attempt_id in sorted(overlapping)
    )
    resolved = add_history([row for row in rows if row["attempt_id"] not in overlapping])
    resolved.sort(key=lambda row: (
        row["player_id"], row["application_session_id"], row["event_sequence"], row["attempt_id"]
    ))
    rejected.sort(key=lambda row: (row.get("attempt_id", ""), row["reason"]))
    return resolved, rejected


def _add_session_history(session_rows: list[Attempt]) -> list[Attempt]:
    session_rows.sort(key=lambda row: (row["event_sequence"], row["attempt_id"]))
    observed_attempts = 0
    observed_failures = 0
    segment_failures: dict[str, int] = defaultdict(int)
    active_seconds = 0.0
    previous_outcome = "none_observed"
    started_attempts = 0
    first_exposure_count: dict[tuple[str, str], int] = defaultdict(int)
    ending_attempts = sorted(
        (row for row in session_rows if row["end_sequence"] is not None),
        key=lambda row: (row["end_sequence"], row["attempt_id"]),
    )
    end_index = 0

    for row in session_rows:
        while (
            end_index < len(ending_attempts)
            and ending_attempts[end_index]["end_sequence"] < row["event_sequence"]
        ):
            ended = ending_attempts[end_index]
            if ended["label"] is not None:
                observed_attempts += 1
                previous_outcome = ended["outcome"]
                if ended["label"] == 1:
                    observed_failures += 1
                    segment_failures[ended["segment_id"]] += 1
                active_seconds += ended["duration_seconds"] or 0
            end_index += 1

        segment_key = (row["segment_id"], row["segment_version"])
        first_exposure_count[segment_key] += 1
        row.update(
            prior_observed_attempts=started_attempts,
            prior_observed_failures=observed_failures,
            prior_segment_observed_failures=segment_failures[row["segment_id"]],
            prior_active_seconds=active_seconds,
            previous_outcome=previous_outcome,
            history_incomplete=int(row["attempt_number"] != first_exposure_count[segment_key]),
        )
        started_attempts += 1
    return session_rows


def add_history(rows: Sequence[Attempt]) -> list[Attempt]:
    rows_by_session: dict[tuple[str, str], list[Attempt]] = defaultdict(list)
    for row in rows:
        rows_by_session[(row["player_id"], row["application_session_id"])].append(row)

    output = []
    for session_key in sorted(rows_by_session):
        output.extend(_add_session_history(rows_by_session[session_key]))
    return output

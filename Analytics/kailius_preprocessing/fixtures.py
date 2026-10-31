def make_segment():
    return {
        "segment_id": "a",
        "segment_version": "1",
        "geometry_hash": "hash",
        "has_difficulty_target": True,
        "target_failure_min": 0.2,
        "target_failure_max": 0.5,
        "geometry": {
            "start": {"x": 0, "y": 0},
            "target": {"x": 10, "y": 0},
            "platforms": [
                {"x": 0, "y": 0, "width": 2, "height": 1},
                {"x": 10, "y": 0, "width": 2, "height": 1},
            ],
            "hazards": [],
        },
    }


def make_attempt_event(attempt_id="attempt", ended=False, player_id="player", sequence=None):
    return {
        "event_id": attempt_id + ("-end" if ended else "-start"),
        "event_name": "segment_attempt_ended" if ended else "segment_attempt_started",
        "event_timestamp": "2026-10-31T12:00:00+00:00",
        "environment": "production",
        "schema_version": 1,
        "application_session_id": player_id + "-session",
        "elapsed_seconds": 10 if ended else 0,
        "event_sequence": sequence if sequence is not None else (2 if ended else 1),
        "player_id": player_id,
        "attempt_id": attempt_id,
        "segment_id": "a",
        "segment_version": "1",
        "geometry_hash": "hash",
        "attempt_number": 1,
        "health": 200,
        "gravity": -9.81,
        "move_speed": 5,
        "platform": "ANDROID",
        "input_method": "touch",
        "start_reason": "entry",
        "offer_variant": "A",
        **({"outcome": "failure", "reason": "hazard", "duration_seconds": 10} if ended else {}),
    }

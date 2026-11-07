import argparse
import csv
import json
from pathlib import Path
from kailius_preprocessing.fixtures import make_attempt_event, make_segment


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    if any(args.output.iterdir()):
        raise ValueError("Choose an empty example directory")
    segment = make_segment()
    segment.update(segment_id="synthetic-route", segment_version="synthetic-1", geometry_hash="synthetic-hash")
    (args.output / "catalogue.json").write_text(json.dumps({"schema_version": 1, "segments": [segment]}, indent=2))
    records = []
    reference = json.loads((Path(__file__).resolve().parents[1] / "event-schema.reference.json").read_text())
    parameter_names = {event["name"]: {parameter["name"] for parameter in event["parameters"]} for event in reference["events"]}
    for player in range(200):
        for end in (False, True):
            event = make_attempt_event(f"synthetic-attempt-{player}", end, f"synthetic-player-{player}")
            event.update(level_segment_id=segment["segment_id"], segment_version=segment["segment_version"], geometry_hash=segment["geometry_hash"])
            parameters = {name: value for name, value in event.items() if name in parameter_names[event["event_name"]]}
            if end:
                parameters.update(outcome="failure" if player % 2 else "success", reason="synthetic", duration_seconds=10)
            else:
                parameters.update(attempt_number=1, health=200, defense=0, attack_damage=100, move_speed=5,
                                  jump_height=8, gravity=-9.81, start_reason="scene_entry")
            payload = {"eventUUID": event["event_id"], "eventName": event["event_name"],
                       "userID": event["sdk_user_id"], "sessionID": event["sdk_session_id"],
                       "eventTimestamp": f"2026-10-31T12:00:{10 if end else 0:02d}Z", "eventParams": parameters}
            records.append({"EVENT_ID": event["event_id"], "EVENT_NAME": event["event_name"],
                            "EVENT_TIMESTAMP": payload["eventTimestamp"], "USER_ID": event["sdk_user_id"],
                            "CLIENT_VERSION": event["client_version"], "ENVIRONMENT_NAME": "development",
                            "PLATFORM": "ANDROID", "EVENT_JSON": json.dumps(payload)})
    with (args.output / "events.csv").open("w", newline="", encoding="utf-8") as file:
        writer = csv.DictWriter(file, fieldnames=list(records[0]))
        writer.writeheader()
        writer.writerows(records)
    (args.output / "config.json").write_text(json.dumps({"input_globs": ["events.csv"], "catalogue_globs": ["catalogue.json"],
        "environment": "development", "expected_raw_rows": 400, "output_dir": "processed", "seed": 42,
        "minimum_players": 30, "extraction_start_utc": "2026-10-31T12:00:00Z",
        "extraction_end_utc": "2026-10-31T13:00:00Z"}, indent=2))


if __name__ == "__main__":
    main()

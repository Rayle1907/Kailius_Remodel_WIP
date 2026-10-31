import json
import re
from pathlib import Path


CSHARP_TYPES = {"string": "string", "int": "integer", "float": "float", "bool": "boolean"}
COMMON_PARAMETERS = {
    "schema_version": "integer",
    "application_session_id": "string",
    "event_sequence": "integer",
    "elapsed_seconds": "float",
    "build_version": "string",
    "player_id": "string",
    "offer_variant": "string",
    "scene_name": "string",
    "playthrough_id": "string",
    "run_id": "string",
}


def class_contract(source, class_name):
    marker = "class " + class_name
    start = source.find(marker)
    if start < 0:
        raise ValueError("C# event class is missing: " + class_name)
    next_class = source.find("\ninternal sealed class ", start + len(marker))
    if next_class < 0:
        next_class = len(source)
    body = source[start:next_class]
    constructor = re.search(r": base\(\"([^\"]+)\"(?:,\s*[^)]*)?\)", body)
    parameters = {}
    for value_type, parameter_name in re.findall(
            r"public\s+(string|int|float|bool)\s+\w+\s*\{\s*set\s*=>\s*SetParameter\(\"([^\"]+)\"", body):
        parameters[parameter_name] = CSHARP_TYPES[value_type]
    return (constructor.group(1) if constructor else None), parameters


def validate_contract(reference_path, source_path):
    reference = json.loads(Path(reference_path).read_text(encoding="utf-8"))
    source = Path(source_path).read_text(encoding="utf-8")
    event_classes = {
        "player_death": "PlayerDeathEvent",
        "player_failure": "PlayerFailureEvent",
        "segment_attempt_started": "SegmentAttemptStartedEvent",
        "segment_attempt_ended": "SegmentAttemptEndedEvent",
        "revive_offer_shown": "ReviveOfferShownEvent",
        "revive_offer_resolved": "ReviveOfferResolvedEvent",
        "revive_outcome_observed": "ReviveOutcomeObservedEvent",
    }
    errors = []
    for event_definition in reference["events"]:
        event_name = event_definition["name"]
        class_name = event_classes.get(event_name)
        if class_name is None:
            errors.append("No contract mapping for " + event_name)
            continue
        actual_name, actual_parameters = class_contract(source, class_name)
        if actual_name != event_name:
            errors.append("Event name mismatch for " + class_name)
        declared = dict(COMMON_PARAMETERS)
        declared.update(actual_parameters)
        for parameter in event_definition["parameters"]:
            name = parameter["name"]
            if name not in declared:
                errors.append(event_name + " is missing " + name)
            elif declared[name] != parameter["type"]:
                errors.append(event_name + " has wrong type for " + name)
        if event_name in ("segment_attempt_started", "segment_attempt_ended"):
            for identity in ("attempt_id", "segment_id", "segment_version", "geometry_hash"):
                if not any(item["name"] == identity and item["required"] for item in event_definition["parameters"]):
                    errors.append(event_name + " must require " + identity)
    if errors:
        raise ValueError("Event schema contract mismatch: " + "; ".join(errors))
    return True

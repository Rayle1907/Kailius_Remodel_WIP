import json
import re
from pathlib import Path


CSHARP_TYPES = {"string": "string", "int": "integer", "float": "float", "bool": "boolean"}
COMMON_PARAMETERS = {
    "schema_version": "integer",
    "event_sequence": "integer",
    "time_since_session_start": "float",
    "offer_variant": "string",
    "experiment_version": "string",
    "scene_name": "string",
    "playthrough_id": "string",
    "run_id": "string",
    "gauntlet_session_id": "string",
}
SURVEY_PARAMETERS = {
    "survey_id": "string",
    "survey_version": "string",
    "playthrough_id": "string",
    "offer_variant": "string",
    "experiment_version": "string",
    "scene_name": "string",
}
SURVEY_SUBMISSION_PARAMETERS = {
    "survey_answered_count": "integer",
    "survey_pxi_answered_count": "integer",
    "survey_complete": "boolean",
}
EVENT_CLASSES = {
    "player_death": "PlayerDeathEvent",
    "player_failure": "PlayerFailureEvent",
    "segment_attempt_started": "SegmentAttemptStartedEvent",
    "segment_attempt_ended": "SegmentAttemptEndedEvent",
    "revive_offer_shown": "ReviveOfferShownEvent",
    "revive_offer_resolved": "ReviveOfferResolvedEvent",
    "revive_outcome_observed": "ReviveOutcomeObservedEvent",
    "gauntlet_run_started": "GauntletRunStartedEvent",
    "gauntlet_run_ended": "GauntletRunEndedEvent",
    "survey_invitation_shown": "SurveyInvitationShownEvent",
    "survey_invitation_resolved": "SurveyInvitationResolvedEvent",
    "survey_submitted": "SurveySubmittedEvent",
}


def _class_body(source, class_name):
    declaration = re.search(r"\bclass\s+" + re.escape(class_name) + r"\b([^\{]*)\{", source)
    if declaration is None:
        raise ValueError("C# event class is missing: " + class_name)
    depth = 1
    index = declaration.end()
    start = index
    while index < len(source) and depth:
        if source[index] == "{":
            depth += 1
        elif source[index] == "}":
            depth -= 1
        index += 1
    if depth:
        raise ValueError("C# event class is incomplete: " + class_name)
    return declaration.group(1).strip(), source[start:index - 1]


def class_contract(source, class_name):
    declaration, body = _class_body(source, class_name)
    constructor = re.search(r': base\("([^\"]+)"(?:,\s*[^)]*)?\)', body)
    parameters = {}
    for value_type, parameter_name in re.findall(
            r'public\s+(string|int|float|bool)\s+\w+\s*\{\s*set\s*=>\s*SetParameter\("([^\"]+)"', body):
        parameters[parameter_name] = CSHARP_TYPES[value_type]
    for parameter_name, value in re.findall(r'SetParameter\("([^\"]+)",\s*([^,)]+)', body):
        if parameter_name in parameters:
            continue
        if '"' in value:
            parameters[parameter_name] = "string"
        elif value.strip() in ("true", "false"):
            parameters[parameter_name] = "boolean"
    return (constructor.group(1) if constructor else None), parameters


def _survey_parameter_names(source_path):
    response_path = Path(source_path).with_name("ResearchSurveyResponse.cs")
    source = response_path.read_text(encoding="utf-8")
    match = re.search(r"parameterNames\s*=\s*\{([^}]+)\}", source)
    if match is None:
        raise ValueError("Survey parameter names are missing from ResearchSurveyResponse.cs")
    return re.findall(r'"([^\"]+)"', match.group(1))


def validate_contract(reference_path, source_path):
    reference = json.loads(Path(reference_path).read_text(encoding="utf-8"))
    source = Path(source_path).read_text(encoding="utf-8")
    survey_answers = {name: "integer" for name in _survey_parameter_names(source_path)}
    telemetry_classes = {
        "PlayerDeathEvent", "PlayerFailureEvent", "SegmentAttemptStartedEvent",
        "SegmentAttemptEndedEvent", "ReviveOfferShownEvent", "ReviveOfferResolvedEvent",
        "ReviveOutcomeObservedEvent", "GauntletRunStartedEvent", "GauntletRunEndedEvent",
        "SurveyInvitationShownEvent", "SurveyInvitationResolvedEvent", "SurveySubmittedEvent",
    }
    errors = []
    version = re.search(r'public\s+int\s+SchemaVersion\s*=>\s*(\d+);', source)
    if version is None or int(version.group(1)) != reference["schema_version"]:
        errors.append("Reference schema version differs from the C# context")
    _, common_body = _class_body(source, "ResearchTelemetryEvent")
    context_types = {
        name: CSHARP_TYPES[value_type]
        for value_type, name in re.findall(r'public\s+(string|int|float|bool)\s+(\w+)\s*(?:\{\s*get;\s*\}|=>)', source)
    }
    actual_common = {
        name: context_types.get(property_name)
        for name, property_name in re.findall(r'SetParameter\("([^\"]+)",\s*context\.(\w+)\)', common_body)
    }
    if actual_common != COMMON_PARAMETERS:
        errors.append("Shared telemetry parameters differ from the C# context")
    seen_events = set()
    for event_definition in reference["events"]:
        event_name = event_definition["name"]
        seen_events.add(event_name)
        class_name = EVENT_CLASSES.get(event_name)
        if class_name is None:
            errors.append("No contract mapping for " + event_name)
            continue
        actual_name, actual_parameters = class_contract(source, class_name)
        if actual_name != event_name:
            errors.append("Event name mismatch for " + class_name)
        declared = dict(COMMON_PARAMETERS) if class_name in telemetry_classes else {}
        if class_name.startswith("Survey"):
            declared.update(SURVEY_PARAMETERS)
            if class_name == "SurveySubmittedEvent":
                declared.update(SURVEY_SUBMISSION_PARAMETERS)
                declared.update(survey_answers)
        declared.update(actual_parameters)
        schema_parameters = {parameter["name"]: parameter["type"] for parameter in event_definition["parameters"]}
        for parameter in event_definition["parameters"]:
            expected_required = parameter["name"] not in survey_answers
            if parameter["required"] != expected_required:
                errors.append(event_name + " has incorrect required status for " + parameter["name"])
        for name, value_type in declared.items():
            if schema_parameters.get(name) != value_type:
                errors.append(event_name + " is missing or has wrong type for " + name)
        for name in schema_parameters:
            if name not in declared:
                errors.append(event_name + " declares unknown parameter " + name)
        if len(schema_parameters) != len(event_definition["parameters"]):
            errors.append(event_name + " contains duplicate parameter declarations")
        if event_name in ("segment_attempt_started", "segment_attempt_ended"):
            for identity in ("attempt_id", "level_segment_id", "segment_version", "geometry_hash"):
                if not any(item["name"] == identity and item["required"] for item in event_definition["parameters"]):
                    errors.append(event_name + " must require " + identity)
    for event_name in EVENT_CLASSES:
        if event_name not in seen_events:
            errors.append("Event is missing from reference: " + event_name)
    if errors:
        raise ValueError("Event schema contract mismatch: " + "; ".join(errors))
    return True

import math
from collections import Counter, defaultdict

def revive_journeys(events):
    groups = defaultdict(list)
    for event in events:
        if event["event_name"].startswith("revive_") and event.get("offer_id"):
            groups[event["offer_id"]].append(event)
    result = []
    for offer_id, records in sorted(groups.items()):
        row = {"offer_id": offer_id, "response": None, "post_decision_outcome": None}
        issues = []
        identity_fields = ("player_id", "attempt_id")
        for field in identity_fields:
            values = {event.get(field) for event in records if event.get(field)}
            if len(values) > 1:
                issues.append("conflicting_" + field)
        row["attempt_id"] = next((event.get("attempt_id") for event in records if event.get("attempt_id")), None)
        for event_name, prefix in (("revive_offer_shown", "shown"), ("revive_offer_resolved", "resolved"), ("revive_outcome_observed", "outcome")):
            matching = [event for event in records if event["event_name"] == event_name]
            if len(matching) > 1:
                issues.append("conflicting_" + prefix + "_events")
            if len(matching) == 1:
                event = matching[0]
                row.update({prefix + "_" + key: value for key, value in event.items()})
                if prefix == "resolved":
                    row["response"] = event.get("offer_response")
                if prefix == "outcome":
                    row["post_decision_outcome"] = event.get("post_decision_outcome")
            elif len(matching) > 1:
                row[prefix + "_events"] = matching
        if "conflicting_resolved_events" in issues or any(issue.startswith("conflicting_") and issue.endswith("_id") for issue in issues):
            row["response"] = None
        if "conflicting_outcome_events" in issues or any(issue.startswith("conflicting_") and issue.endswith("_id") for issue in issues):
            row["post_decision_outcome"] = None
        row["quality"] = "valid" if not issues else ";".join(sorted(issues))
        result.append(row)
    return result

def wilson(failures, count):
    z = 1.959963984540054
    rate = failures / count
    divisor = 1 + z * z / count
    centre = (rate + z * z / (2 * count)) / divisor
    half = z * math.sqrt(rate * (1 - rate) / count + z * z / (4 * count * count)) / divisor
    return centre - half, centre + half

def difficulty_report(rows, segments, minimum):
    identity_fields = ("segment_id", "segment_version", "geometry_hash")
    result = []
    for identity, segment in sorted(segments.items()):
        cohort = [row for row in rows if row["first_attempt"]
                  and tuple(row[field] for field in identity_fields) == identity]
        players = Counter(row["player_id"] for row in cohort)
        cohort = [row for row in cohort if players[row["player_id"]] == 1]
        resolved = [row for row in cohort if row["label"] is not None]
        failures = sum(row["label"] for row in resolved)
        low, high = wilson(failures, len(resolved)) if resolved else (None, None)
        status = "insufficient_evidence"
        has_target = segment.get("has_difficulty_target", False)
        if len(resolved) >= minimum and has_target:
            if low > segment["target_failure_max"]:
                status = "too_hard"
            elif high < segment["target_failure_min"]:
                status = "too_easy"
            else:
                status = "no_confirmed_mismatch"
        result.append({
            "segment_id": identity[0],
            "segment_version": identity[1],
            "geometry_hash": identity[2],
            "resolved_first_attempts": len(resolved), "failures": failures, "successes": len(resolved) - failures,
            "unresolved_first_attempts": len(cohort) - len(resolved), "unique_players": len(cohort),
            "failure_probability": failures / len(resolved) if resolved else None,
            "ci95_low": low, "ci95_high": high, "status": status,
            "target_failure_min": segment.get("target_failure_min") if has_target else None,
            "target_failure_max": segment.get("target_failure_max") if has_target else None,
        })
    return result

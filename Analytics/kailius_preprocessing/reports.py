import math
from collections import Counter, defaultdict


IDENTITY_FIELDS = (
    "player_id", "application_session_id", "playthrough_id", "gauntlet_session_id", "run_id", "attempt_id"
)
RUN_IDENTITY_FIELDS = (
    "player_id", "application_session_id", "gauntlet_session_id", "gauntlet_id", "run_id",
    "playthrough_id", "offer_variant", "experiment_version",
)
SURVEY_IDENTITY_FIELDS = (
    "player_id", "application_session_id", "gauntlet_session_id", "run_id", "survey_version",
    "playthrough_id", "offer_variant", "experiment_version", "scene_name",
)
SURVEY_ANSWER_FIELDS = (
    "pxi_autonomy_1", "pxi_autonomy_2", "pxi_autonomy_3",
    "pxi_mastery_1", "pxi_mastery_2", "pxi_mastery_3",
    "pxi_challenge_1", "pxi_challenge_2", "pxi_challenge_3",
    "pxi_progress_feedback_1", "pxi_progress_feedback_2", "pxi_progress_feedback_3",
    "background_experience_skill_1", "background_experience_skill_2", "background_experience_skill_3",
)


def _identity_issues(records, fields):
    issues = []
    for field in fields:
        values = {record.get(field) for record in records if record.get(field) not in (None, "")}
        if len(values) > 1:
            if field == "application_session_id":
                issues.append("sdk_session_transition")
            else:
                issues.append("conflicting_" + field)
    return issues


def _missing_identity_issues(records, fields):
    return [
        "missing_" + field
        for field in fields
        if any(record.get(field) in (None, "") for record in records)
    ]


def _observation_issues(records):
    return [
        "missing_" + field
        for field in ("schema_version", "event_sequence", "time_since_session_start")
        if any(record.get(field) in (None, "") for record in records)
    ]


def _ordered_before(first, second):
    first_session = first.get("application_session_id")
    second_session = second.get("application_session_id")
    first_sequence = first.get("event_sequence")
    second_sequence = second.get("event_sequence")
    if first_session and first_session == second_session and first_sequence not in (None, "") and second_sequence not in (None, ""):
        return int(first_sequence) < int(second_sequence)
    first_timestamp = first.get("event_timestamp")
    second_timestamp = second.get("event_timestamp")
    if first_timestamp and second_timestamp and first_timestamp != second_timestamp:
        return first_timestamp < second_timestamp
    return None


def _single_stage(records, event_name, prefix, row, issues):
    matching = [event for event in records if event["event_name"] == event_name]
    if len(matching) > 1:
        issues.append("conflicting_" + prefix + "_events")
    elif matching:
        row[prefix + "_event_id"] = matching[0].get("event_id")
        return matching[0]
    return None


def _status(issues):
    if any(issue.startswith(("conflicting_", "invalid_")) or issue in {
        "outcome_without_resolution", "submission_after_decline",
    } or issue.startswith("orphan_") for issue in issues):
        return "conflicting"
    return "incomplete" if issues else "complete"


def revive_journeys(events, attempts=None, run_ids=None):
    groups = defaultdict(list)
    for event in events:
        if not event["event_name"].startswith("revive_"):
            continue
        if not event.get("offer_id"):
            continue
        groups[event["offer_id"]].append(event)

    known_attempts = None if attempts is None else {attempt.get("attempt_id") for attempt in attempts}
    known_runs = None if run_ids is None else set(run_ids)
    result = []
    for offer_id, records in sorted(groups.items()):
        row = {"offer_id": offer_id, "offer_response": None, "post_decision_outcome": None}
        issues = _identity_issues(records, IDENTITY_FIELDS)
        issues.extend(_observation_issues(records))
        shown = _single_stage(records, "revive_offer_shown", "shown", row, issues)
        resolved = _single_stage(records, "revive_offer_resolved", "resolved", row, issues)
        outcome = _single_stage(records, "revive_outcome_observed", "outcome", row, issues)
        row["attempt_id"] = next((event.get("attempt_id") for event in records if event.get("attempt_id")), None)
        row["run_id"] = next((event.get("run_id") for event in records if event.get("run_id")), None)
        row["player_id"] = next((event.get("player_id") for event in records if event.get("player_id")), None)
        issues.extend(_missing_identity_issues(records, IDENTITY_FIELDS))
        for prefix, event in (("shown", shown), ("resolved", resolved), ("outcome", outcome)):
            if event is not None:
                row.update({prefix + "_" + key: value for key, value in event.items()})
        if shown is None:
            issues.append("missing_shown_event")
        if resolved is None:
            issues.append("missing_resolved_event")
        if outcome is not None and resolved is None:
            issues.append("outcome_without_resolution")
        offer_response = resolved.get("offer_response") if resolved else None
        if resolved is not None and offer_response in (None, ""):
            issues.append("missing_offer_response")
        elif resolved is not None and offer_response not in ("accepted", "declined", "ignored", "quit"):
            issues.append("invalid_offer_response")
        if resolved is not None and offer_response != "quit" and outcome is None:
            issues.append("missing_outcome_event")
        outcome_value = outcome.get("post_decision_outcome") if outcome else None
        if outcome is not None and outcome_value in (None, ""):
            issues.append("missing_post_decision_outcome")
        if shown is not None and resolved is not None:
            order = _ordered_before(shown, resolved)
            if order is False:
                issues.append("invalid_shown_resolved_order")
            elif order is None:
                issues.append("unknown_shown_resolved_order")
        if resolved is not None and outcome is not None:
            order = _ordered_before(resolved, outcome)
            if order is False:
                issues.append("invalid_resolved_outcome_order")
            elif order is None:
                issues.append("unknown_resolved_outcome_order")
        identity_conflict = any(issue.startswith("conflicting_") for issue in issues)
        invalid_chain = any(issue.startswith("invalid_") or issue.endswith("_without_resolution") for issue in issues)
        if resolved is not None and offer_response not in (None, "") and not identity_conflict and not invalid_chain and "conflicting_resolved_events" not in issues:
            row["offer_response"] = offer_response
        if outcome is not None and outcome_value not in (None, "") and not identity_conflict and not invalid_chain and "conflicting_outcome_events" not in issues:
            row["post_decision_outcome"] = outcome_value
        if known_attempts is not None and row["attempt_id"] and row["attempt_id"] not in known_attempts:
            issues.append("orphan_attempt_link")
        if known_runs is not None and row["run_id"] and row["run_id"] not in known_runs:
            issues.append("orphan_run_link")
        row["quality"] = ";".join(sorted(set(issues))) if issues else "valid"
        row["status"] = _status(issues)
        result.append(row)
    return result


def run_journeys(events):
    groups = defaultdict(list)
    missing_run_id = 0
    for event in events:
        if event["event_name"] not in ("gauntlet_run_started", "gauntlet_run_ended"):
            continue
        if not event.get("run_id"):
            missing_run_id += 1
            continue
        groups[event["run_id"]].append(event)
    result = []
    for run_id, records in sorted(groups.items()):
        row = {"run_id": run_id}
        issues = _identity_issues(records, RUN_IDENTITY_FIELDS)
        issues.extend(_observation_issues(records))
        issues.extend(_missing_identity_issues(records, RUN_IDENTITY_FIELDS))
        started = _single_stage(records, "gauntlet_run_started", "started", row, issues)
        ended = _single_stage(records, "gauntlet_run_ended", "ended", row, issues)
        if started is None:
            issues.append("missing_run_start")
        if ended is None:
            issues.append("missing_run_end")
        if started is not None and ended is not None:
            order = _ordered_before(started, ended)
            if order is False:
                issues.append("invalid_run_order")
            elif order is None:
                issues.append("unknown_run_order")
        for field in RUN_IDENTITY_FIELDS:
            row[field] = next((event.get(field) for event in records if event.get(field) not in (None, "")), None)
        row["run_end_reason"] = ended.get("run_end_reason") if ended else None
        row["run_start_reason"] = started.get("run_start_reason") if started else None
        row["run_duration_seconds"] = ended.get("run_duration_seconds") if ended else None
        row["run_deaths"] = ended.get("run_deaths") if ended else None
        row["accepted_revives"] = ended.get("accepted_revives") if ended else None
        row["premium_currency_balance"] = ended.get("premium_currency_balance") if ended else None
        row["offers_shown_in_gauntlet"] = (ended or started).get("offers_shown_in_gauntlet")
        row["next_revive_price_int"] = (ended or started).get("next_revive_price_int")
        row["exit_checkpoint_id"] = ended.get("exit_checkpoint_id") if ended else None
        row["exit_health"] = ended.get("exit_health") if ended else None
        row["defeated_required_enemies"] = ended.get("defeated_required_enemies") if ended else None
        row["total_required_enemies"] = ended.get("total_required_enemies") if ended else None
        row["quality"] = ";".join(sorted(set(issues))) if issues else "valid"
        row["status"] = _status(issues)
        result.append(row)
    return result


def survey_journeys(events, run_ids=None):
    groups = defaultdict(list)
    missing_survey_id = 0
    names = ("survey_invitation_shown", "survey_invitation_resolved", "survey_submitted")
    for event in events:
        if event["event_name"] not in names:
            continue
        if not event.get("survey_id"):
            missing_survey_id += 1
            continue
        groups[event["survey_id"]].append(event)
    result = []
    for survey_id, records in sorted(groups.items()):
        row = {"survey_id": survey_id}
        issues = _identity_issues(records, SURVEY_IDENTITY_FIELDS)
        issues.extend(_observation_issues(records))
        issues.extend(_missing_identity_issues(records, SURVEY_IDENTITY_FIELDS + ("survey_id",)))
        shown = _single_stage(records, names[0], "invitation", row, issues)
        resolved = _single_stage(records, names[1], "resolution", row, issues)
        submitted = _single_stage(records, names[2], "submission", row, issues)
        if shown is None:
            issues.append("missing_invitation")
        if resolved is None:
            issues.append("missing_invitation_resolution")
        survey_invitation_response = resolved.get("survey_invitation_response") if resolved else None
        if survey_invitation_response == "":
            survey_invitation_response = None
        if resolved is not None and survey_invitation_response is None:
            issues.append("missing_invitation_response")
        elif survey_invitation_response not in (None, "accepted", "declined"):
            issues.append("invalid_invitation_response")
        if survey_invitation_response == "accepted" and submitted is None:
            issues.append("missing_submission")
        if survey_invitation_response == "declined" and submitted is not None:
            issues.append("submission_after_decline")
        if shown is not None and resolved is not None:
            order = _ordered_before(shown, resolved)
            if order is False:
                issues.append("invalid_invitation_order")
            elif order is None:
                issues.append("unknown_invitation_order")
        if resolved is not None and submitted is not None:
            order = _ordered_before(resolved, submitted)
            if order is False:
                issues.append("invalid_submission_order")
            elif order is None:
                issues.append("unknown_submission_order")
        row["submission_count"] = sum(event["event_name"] == "survey_submitted" for event in records)
        if row["submission_count"] > 1:
            issues.append("conflicting_submissions")
        row["playthrough_id"] = next((event.get("playthrough_id") for event in records if event.get("playthrough_id")), None)
        row["player_id"] = next((event.get("player_id") for event in records if event.get("player_id")), None)
        row["application_session_id"] = next((event.get("application_session_id") for event in records if event.get("application_session_id")), None)
        row["gauntlet_session_id"] = next((event.get("gauntlet_session_id") for event in records if event.get("gauntlet_session_id")), None)
        row["run_id"] = next((event.get("run_id") for event in records if event.get("run_id")), None)
        row["offer_variant"] = next((event.get("offer_variant") for event in records if event.get("offer_variant")), None)
        row["experiment_version"] = next((event.get("experiment_version") for event in records if event.get("experiment_version")), None)
        row["survey_version"] = next((event.get("survey_version") for event in records if event.get("survey_version")), None)
        row["scene_name"] = next((event.get("scene_name") for event in records if event.get("scene_name")), None)
        row["survey_invitation_response"] = survey_invitation_response
        row["survey_answered_count"] = submitted.get("survey_answered_count") if submitted else None
        row["survey_pxi_answered_count"] = submitted.get("survey_pxi_answered_count") if submitted else None
        row["survey_complete"] = submitted.get("survey_complete") if submitted else None
        for field in SURVEY_ANSWER_FIELDS:
            row[field] = submitted.get(field) if submitted else None
        if submitted is not None:
            answered_count = sum(submitted.get(field) not in (None, "") for field in SURVEY_ANSWER_FIELDS)
            pxi_answered_count = sum(submitted.get(field) not in (None, "") for field in SURVEY_ANSWER_FIELDS[:12])
            if submitted.get("survey_answered_count") != answered_count:
                issues.append("invalid_survey_answered_count")
            if submitted.get("survey_pxi_answered_count") != pxi_answered_count:
                issues.append("invalid_survey_pxi_answered_count")
            if submitted.get("survey_complete") != (answered_count == len(SURVEY_ANSWER_FIELDS)):
                issues.append("invalid_survey_completeness_flag")
            if any(
                value not in (None, "") and (not isinstance(value, (int, float)) or value < -3 or value > 3)
                for value in (submitted.get(field) for field in SURVEY_ANSWER_FIELDS)
            ):
                issues.append("invalid_answer_value")
        row["quality"] = ";".join(sorted(set(issues))) if issues else "valid"
        if run_ids is not None and row["run_id"] and row["run_id"] not in run_ids:
            issues.append("orphan_run_link")
            row["quality"] = ";".join(sorted(set(issues)))
        row["status"] = _status(issues)
        result.append(row)
    return result


def revive_quality(events, journeys):
    missing_offer_id = sum(
        event["event_name"].startswith("revive_") and not event.get("offer_id")
        for event in events
    )
    return {
        "journeys": len(journeys),
        "status_counts": dict(Counter(row["status"] for row in journeys)),
        "incomplete_journeys": sum(row["status"] == "incomplete" for row in journeys),
        "conflicting_journeys": sum(row["status"] == "conflicting" for row in journeys),
        "quality_counts": dict(Counter(issue for row in journeys for issue in row["quality"].split(";") if issue != "valid")),
        "events_missing_offer_id": missing_offer_id,
        "orphan_attempt_links": sum("orphan_attempt_link" in row["quality"].split(";") for row in journeys),
        "orphan_run_links": sum("orphan_run_link" in row["quality"].split(";") for row in journeys),
    }


def survey_quality(journeys):
    submitted = [row for row in journeys if row.get("submission_count", 0) > 0]
    return {
        "journeys": len(journeys),
        "submitted": len(submitted),
        "complete_submissions": sum(row["survey_complete"] is True for row in submitted),
        "partial_submissions": sum(row["survey_complete"] is False for row in submitted),
        "submissions_missing_survey_completeness": sum(row["survey_complete"] is None for row in submitted),
        "unsubmitted_accepted": sum(row["survey_invitation_response"] == "accepted" and row.get("submission_count", 0) == 0 for row in journeys),
        "submissions_missing_survey_answered_count": sum(row["survey_answered_count"] is None for row in submitted),
        "survey_answered_count_distribution": dict(Counter(str(row["survey_answered_count"]) for row in submitted if row["survey_answered_count"] is not None)),
        "status_counts": dict(Counter(row["status"] for row in journeys)),
        "incomplete_journeys": sum(row["status"] == "incomplete" for row in journeys),
        "conflicting_journeys": sum(row["status"] == "conflicting" for row in journeys),
        "quality_counts": dict(Counter(issue for row in journeys for issue in row["quality"].split(";") if issue != "valid")),
    }


def wilson(failures, count):
    z = 1.959963984540054
    rate = failures / count
    divisor = 1 + z * z / count
    centre = (rate + z * z / (2 * count)) / divisor
    half = z * math.sqrt(rate * (1 - rate) / count + z * z / (4 * count * count)) / divisor
    return centre - half, centre + half


def difficulty_report(rows, segments, minimum):
    identity_fields = ("level_segment_id", "segment_version", "geometry_hash")
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
            "level_segment_id": identity[0],
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

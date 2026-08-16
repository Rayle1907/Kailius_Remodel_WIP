# Kailius research analytics schema

Environment workflow:

1. Create and test these definitions in the `development` environment.
2. Do not manually recreate them in `production`.
3. After validation, select the events in Event Manager and use **Copy to Environment > production**.

Unity Analytics automatically supplies `userID`, `sessionID`, `eventTimestamp`, and `eventUUID`. They are intentionally not duplicated below.

## Shared controlled values

- `offer_variant`: `A` or `B`
- `experiment_version`: `revive_study_v1`
- `offer_response`: `accepted`, `declined`, `ignored`, or `quit`
- `cause_of_death`: `enemy_contact`, `enemy_projectile`, `trap`, `fall`, `boss`, `environmental`, or `unknown`
- `post_decision_outcome`: `gauntlet_completed`, `died_again`, `run_restarted`, `returned_to_menu`, or `application_quit`

## gauntlet_run_started

| Parameter | Unity type |
| --- | --- |
| `run_id` | string |
| `gauntlet_id` | string |
| `scene_name` | string |
| `level_segment_id` | string |
| `premium_currency_balance` | int |
| `offer_variant` | string |
| `experiment_version` | string |

## player_death

| Parameter | Unity type |
| --- | --- |
| `run_id` | string |
| `scene_name` | string |
| `level_segment_id` | string |
| `death_number_total` | int |
| `death_number_in_segment` | int |
| `time_since_session_start` | double |
| `time_since_last_death` | double |
| `cause_of_death` | string |
| `player_health_before_death` | int |
| `premium_currency_balance` | int |
| `offer_eligible` | bool |
| `offer_shown` | bool |
| `offer_variant` | string |
| `experiment_version` | string |

`offer_eligible` is the deterministic schedule result. `offer_shown` is false until an actual offer UI calls `RecordOfferShown()`; eligibility must not be misreported as exposure.

## revive_offer_shown

| Parameter | Unity type |
| --- | --- |
| `run_id` | string |
| `offer_id` | string |
| `scene_name` | string |
| `level_segment_id` | string |
| `death_number_total` | int |
| `death_number_in_segment` | int |
| `premium_currency_balance` | int |
| `revive_price` | int |
| `offer_number_in_gauntlet` | int |
| `can_afford` | bool |
| `offer_variant` | string |
| `experiment_version` | string |

## revive_offer_resolved

| Parameter | Unity type |
| --- | --- |
| `run_id` | string |
| `offer_id` | string |
| `offer_response` | string |
| `time_to_decision` | double |
| `revive_price` | int |
| `premium_currency_balance` | int |
| `balance_after_decision` | int |
| `offer_variant` | string |
| `experiment_version` | string |

## revive_outcome_observed

| Parameter | Unity type |
| --- | --- |
| `run_id` | string |
| `offer_id` | string |
| `post_decision_outcome` | string |
| `seconds_after_decision` | double |
| `scene_name` | string |
| `level_segment_id` | string |
| `offer_variant` | string |
| `experiment_version` | string |

## gauntlet_run_ended

| Parameter | Unity type |
| --- | --- |
| `run_id` | string |
| `gauntlet_id` | string |
| `run_end_reason` | string |
| `run_duration_seconds` | double |
| `run_deaths` | int |
| `accepted_revives` | int |
| `premium_currency_balance` | int |
| `offer_variant` | string |
| `experiment_version` | string |

## Current implementation boundary

The repository now records run starts and lethal damage. The event classes and offer state machine exist, but there is not yet a revive-offer UI wired to:

- `GauntletRunTracker.RecordOfferShown()`
- `GauntletRunTracker.ResolveOffer(...)`
- `GauntletRunTracker.RecordPostDecisionOutcome(...)`

Do not interpret missing offer events as player rejection until that UI integration is implemented and tested.

WITH extraction_parameters AS (
    SELECT TO_TIMESTAMP_TZ('2026-11-01T00:00:00Z') AS extraction_start_utc,
           TO_TIMESTAMP_TZ('2026-12-01T00:00:00Z') AS extraction_end_utc,
           'development' AS extraction_environment
)
SELECT COALESCE(e.EVENT_JSON:eventUUID::STRING, e.EVENT_ID::STRING) AS EVENT_ID,
       e.EVENT_NAME, e.EVENT_TIMESTAMP, e.USER_ID,
       p.extraction_environment AS ENVIRONMENT_NAME, e.PLATFORM, e.CLIENT_VERSION, e.EVENT_JSON
FROM EVENTS AS e
CROSS JOIN extraction_parameters AS p
WHERE e.EVENT_TIMESTAMP >= p.extraction_start_utc
  AND e.EVENT_TIMESTAMP < p.extraction_end_utc
  AND e.EVENT_LEVEL = 0
  AND e.EVENT_NAME IN ('segment_attempt_started', 'segment_attempt_ended',
                     'player_death', 'player_failure', 'revive_offer_shown',
                     'revive_offer_resolved', 'revive_outcome_observed',
                     'gauntlet_run_started', 'gauntlet_run_ended',
                     'survey_invitation_shown', 'survey_invitation_resolved',
                     'survey_submitted');

SELECT COALESCE(EVENT_JSON:eventUUID::STRING, EVENT_ID::STRING) AS EVENT_ID,
       EVENT_NAME, EVENT_TIMESTAMP, USER_ID,
       UNITY_ENVIRONMENT_NAME AS ENVIRONMENT_NAME, PLATFORM, CLIENT_VERSION, EVENT_JSON
FROM EVENTS
WHERE EVENT_DATE >= '2026-10-01'
  AND EVENT_DATE < '2026-11-01'
  AND UNITY_ENVIRONMENT_NAME = 'production'
  AND EVENT_LEVEL = 0
  AND EVENT_NAME IN ('segment_attempt_started', 'segment_attempt_ended',
                     'player_death', 'player_failure', 'revive_offer_shown',
                     'revive_offer_resolved', 'revive_outcome_observed');

-- One-time move of the hand-written event store (schema es) into Deedbox (schema deedbox).
--
-- Order:
--   1. Stop the old app version.
--   2. Create the Deedbox schema: `deedbox schema apply`, or start the new app once in Development.
--   3. Run this script: psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -f scripts/migrate-es-to-deedbox.sql
--   4. Start the new app version. Deedbox rebuilds the diary, library and lists read models from the events.
--
-- The script runs in one transaction and stops if the Deedbox store already holds events.
-- It leaves schema es in place. Drop it after you verify the migration: DROP SCHEMA es CASCADE;

BEGIN;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM deedbox.events) THEN
        RAISE EXCEPTION 'deedbox.events is not empty. This script runs once, on an empty Deedbox store.';
    END IF;
END
$$;

-- Stream IDs become strings in the standard GUID form, which is what StreamId.From and
-- StreamId.Deterministic produce. Stored state is left empty; Deedbox replays the events on first load.
INSERT INTO deedbox.streams (tenant_id, stream_id, stream_type, version, state, state_version, state_at, created_at, updated_at)
SELECT '', s.stream_id::text, s.stream_type, s.version, NULL, 0, 0, s.created_at, s.updated_at
FROM es.streams s;

-- "tracking.item.wanted.v2" splits into event_type "tracking.item.wanted" and event_version 2.
-- Global positions are renumbered from 1 without gaps, in the old position order.
-- Metadata moves to Deedbox's shape. The user and the title move into headers, which the projections read;
-- a tracked item's title comes from its stream state when the event has no context ID.
INSERT INTO deedbox.events (global_position, event_id, tenant_id, stream_id, version, stream_type,
                            event_type, event_version, payload, metadata, occurred_at)
SELECT row_number() OVER (ORDER BY e.global_position),
       gen_random_uuid(),
       '',
       e.stream_id::text,
       e.version,
       s.stream_type,
       substring(e.event_type FROM '^(.*)\.v[0-9]+$'),
       substring(e.event_type FROM '\.v([0-9]+)$')::integer,
       e.payload,
       jsonb_strip_nulls(jsonb_build_object(
           'correlationId', e.metadata ->> 'correlationId',
           'causationId', e.metadata ->> 'causationId',
           'actor', 'user:' || (e.metadata ->> 'actorId'),
           'headers', jsonb_strip_nulls(jsonb_build_object(
               'userId', coalesce(e.metadata ->> 'userId', e.metadata ->> 'actorId'),
               'titleId', CASE WHEN s.stream_type = 'tracked_item'
                               THEN coalesce(e.metadata ->> 'contextId', s.state ->> 'titleId') END)))),
       e.occurred_at
FROM es.events e
JOIN es.streams s ON s.stream_id = e.stream_id;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM deedbox.events WHERE event_type IS NULL OR event_version IS NULL) THEN
        RAISE EXCEPTION 'An es.events row has an event type without a .vN suffix.';
    END IF;
END
$$;

UPDATE deedbox.position SET value = (SELECT count(*) FROM deedbox.events);

INSERT INTO deedbox.event_types (stream_type, event_type, event_version)
SELECT DISTINCT stream_type, event_type, event_version FROM deedbox.events;

-- The read models are rebuilt from the events. Checkpoints are removed, so Deedbox creates them again
-- at the next start; because the store now holds events, each inline projection starts as rebuilding
-- and applies every event before it switches to inline. A start before this script left them at running.
TRUNCATE tracking.diary_entries, tracking.library_items, tracking.list_items, tracking.lists;
DELETE FROM deedbox.checkpoints;

COMMIT;

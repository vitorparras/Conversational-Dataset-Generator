-- The replicated content (issue 82, decision 0024): the database keeps, for every message of every item, the keyed digest of its text and of its sender, and, for the messages whose content is replicated,
-- the mark that points to the first occurrence. Nothing of the text or of the sender is here in clear: the digests are HMACs under a key of the master key that the application holds, and they do not
-- come undone without it. Nothing is removed or merged: a mark is one more row, and the message it marks stays in its item with its sender, its moment and its conversation.
--
-- Both tables are written once. A row never changes, the owner and a replica session included, and a row is removed only with its batch rejected, in the transaction that records the rejection, as
-- the items and the maps are (0035): a batch that is not accepted leaves no digest and no mark behind. An old message that becomes replicated when another sender says the same text later gets a row
-- in replication_mark and the occurrence stays as it was; a mark once written is never undone.

-- The occurrences: one row for each message of an item, by item and position in its conversation, with the digests, the length of the text (the criterion compares it with a minimum) and the moment
-- the message was said (the first occurrence is the earliest). The item and the batch go together as the maps do, so that a message belongs to the batch of its item.
SELECT public.create_content_table('replication_occurrence',
    $columns$batch_id bigint NOT NULL,
     item_id bigint NOT NULL,
     message_position integer NOT NULL,
     text_digest text NOT NULL,
     sender_digest text NOT NULL,
     text_length integer NOT NULL,
     said_at timestamptz NOT NULL,
     written_in xid8 NOT NULL DEFAULT pg_catalog.pg_current_xact_id(),
     CONSTRAINT replication_occurrence_place UNIQUE (item_id, message_position, domain_id),
     CONSTRAINT replication_occurrence_batch_fkey FOREIGN KEY (batch_id, domain_id) REFERENCES content.ingestion_batch (id, domain_id),
     CONSTRAINT replication_occurrence_item_fkey FOREIGN KEY (item_id, batch_id) REFERENCES content.item_text (id, batch_id),
     CONSTRAINT replication_occurrence_position CHECK (message_position >= 0),
     CONSTRAINT replication_occurrence_length CHECK (text_length >= 0),
     CONSTRAINT replication_occurrence_digests CHECK (text_digest ~ '^[0-9a-f]{64}$' AND sender_digest ~ '^[0-9a-f]{64}$')$columns$);

-- The marks: one row for each occurrence that is replicated, with the first occurrence of its content, which is marked too and points to itself. The batch is the one that brought the occurrence
-- that made the content replicated, which is not always the batch of the marked message: the foreign keys carry the domain, so that a mark never points to the occurrence of another domain.
SELECT public.create_content_table('replication_mark',
    $columns$batch_id bigint NOT NULL,
     item_id bigint NOT NULL,
     message_position integer NOT NULL,
     first_item_id bigint NOT NULL,
     first_message_position integer NOT NULL,
     written_in xid8 NOT NULL DEFAULT pg_catalog.pg_current_xact_id(),
     CONSTRAINT replication_mark_place UNIQUE (item_id, message_position),
     CONSTRAINT replication_mark_batch_fkey FOREIGN KEY (batch_id, domain_id) REFERENCES content.ingestion_batch (id, domain_id),
     CONSTRAINT replication_mark_occurrence_fkey FOREIGN KEY (item_id, message_position, domain_id) REFERENCES content.replication_occurrence (item_id, message_position, domain_id),
     CONSTRAINT replication_mark_first_fkey FOREIGN KEY (first_item_id, first_message_position, domain_id) REFERENCES content.replication_occurrence (item_id, message_position, domain_id)$columns$);

-- Everyone reads, the row policy below decides what; the application writes the columns that are the application's and nothing else, and removes only for the rejection of a batch.
GRANT SELECT ON content.replication_occurrence, content.replication_mark TO PUBLIC;
GRANT INSERT (domain_id, batch_id, item_id, message_position, text_digest, sender_digest, text_length, said_at) ON content.replication_occurrence TO app;
GRANT INSERT (domain_id, batch_id, item_id, message_position, first_item_id, first_message_position) ON content.replication_mark TO app;
GRANT DELETE ON content.replication_occurrence, content.replication_mark TO app;

-- Written once: no UPDATE, by any session, and no TRUNCATE. The refusal carries the SQLSTATE of the other refusals of the content (CE006) and names the table and no value.
CREATE FUNCTION public.refuse_replication_change() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    RAISE EXCEPTION 'the occurrences and the marks of replicated content are written once and never change'
        USING ERRCODE = 'CE006', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
END
$refuse$;

CREATE TRIGGER replication_is_written_once BEFORE UPDATE ON content.replication_occurrence FOR EACH ROW EXECUTE FUNCTION public.refuse_replication_change();
CREATE TRIGGER replication_is_written_once BEFORE UPDATE ON content.replication_mark FOR EACH ROW EXECUTE FUNCTION public.refuse_replication_change();
CREATE TRIGGER replication_is_never_truncated BEFORE TRUNCATE ON content.replication_occurrence FOR EACH STATEMENT EXECUTE FUNCTION public.refuse_replication_change();
CREATE TRIGGER replication_is_never_truncated BEFORE TRUNCATE ON content.replication_mark FOR EACH STATEMENT EXECUTE FUNCTION public.refuse_replication_change();

-- Written only to a batch that is open (outside the window of a restore), as the items are; removed only with the batch rejected, as the items are (0035).
CREATE TRIGGER content_needs_open_batch BEFORE INSERT ON content.replication_occurrence FOR EACH ROW EXECUTE FUNCTION public.refuse_content_outside_open_batch();
CREATE TRIGGER content_needs_open_batch BEFORE INSERT ON content.replication_mark FOR EACH ROW EXECUTE FUNCTION public.refuse_content_outside_open_batch();
CREATE TRIGGER content_removal_needs_rejection BEFORE DELETE ON content.replication_occurrence FOR EACH ROW EXECUTE FUNCTION public.refuse_content_removal_without_rejection();
CREATE TRIGGER content_removal_needs_rejection BEFORE DELETE ON content.replication_mark FOR EACH ROW EXECUTE FUNCTION public.refuse_content_removal_without_rejection();

ALTER TABLE content.replication_occurrence ENABLE ALWAYS TRIGGER replication_is_written_once;
ALTER TABLE content.replication_mark ENABLE ALWAYS TRIGGER replication_is_written_once;
ALTER TABLE content.replication_occurrence ENABLE ALWAYS TRIGGER replication_is_never_truncated;
ALTER TABLE content.replication_mark ENABLE ALWAYS TRIGGER replication_is_never_truncated;
ALTER TABLE content.replication_occurrence ENABLE ALWAYS TRIGGER content_needs_open_batch;
ALTER TABLE content.replication_mark ENABLE ALWAYS TRIGGER content_needs_open_batch;
ALTER TABLE content.replication_occurrence ENABLE ALWAYS TRIGGER content_removal_needs_rejection;
ALTER TABLE content.replication_mark ENABLE ALWAYS TRIGGER content_removal_needs_rejection;

-- Visible as the content is (0035): when the batch is accepted; or, while it is open, to the transaction that wrote the row; or to the transaction that rejected the batch; or in the scope of the system,
-- the one of the operations that copy the data. A message of a batch still open or rejected is not compared with by another batch.
CREATE POLICY batch_accepted_only ON content.replication_occurrence AS RESTRICTIVE FOR SELECT
    USING (written_in = pg_catalog.pg_current_xact_id()
        OR EXISTS (SELECT 1 FROM content.ingestion_batch b WHERE b.id = replication_occurrence.batch_id AND b.domain_id = replication_occurrence.domain_id
            AND (b.state = 'accepted' OR (b.state = 'rejected' AND public.batch_event_of_this_transaction(b.id, 'batch_closed', 'batch_rejected', b.motive))))
        OR pg_catalog.current_setting('app.content_scope', true) = 'system');
CREATE POLICY batch_accepted_only ON content.replication_mark AS RESTRICTIVE FOR SELECT
    USING (written_in = pg_catalog.pg_current_xact_id()
        OR EXISTS (SELECT 1 FROM content.ingestion_batch b WHERE b.id = replication_mark.batch_id AND b.domain_id = replication_mark.domain_id
            AND (b.state = 'accepted' OR (b.state = 'rejected' AND public.batch_event_of_this_transaction(b.id, 'batch_closed', 'batch_rejected', b.motive))))
        OR pg_catalog.current_setting('app.content_scope', true) = 'system');

-- The load of a restore puts the two tables back, after the items (the 0034 as it was, with the two tables among the ones it accepts): the occurrences come before the marks that point to them.
CREATE OR REPLACE FUNCTION public.restore_load(table_name text, rows jsonb) RETURNS bigint LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $load$
DECLARE
    target regclass;
    declared integer;
    part bigint;
    loaded bigint := 0;
BEGIN
    PERFORM public.restore_require_open();
    BEGIN
        IF table_name NOT IN ('public.domain', 'public.derivation_label', 'public.derivation_label_revocation', 'public.person', 'public.person_change', 'public.person_credential',
                              'public.sign_in_attempt', 'content.ingestion_batch', 'content.item_text', 'content.reversal_map', 'content.item_emptied',
                              'content.replication_occurrence', 'content.replication_mark') THEN
            RAISE EXCEPTION 'the table is not one the load puts back' USING ERRCODE = 'CE005';
        END IF;

        target := table_name::regclass;
        IF table_name LIKE 'content.%' THEN
            FOR declared IN SELECT DISTINCT (r ->> 'domain_id')::integer FROM jsonb_array_elements(rows) r LOOP
                PERFORM pg_catalog.set_config('app.domain', declared::text, true);
                EXECUTE format('INSERT INTO %s OVERRIDING SYSTEM VALUE SELECT * FROM jsonb_populate_recordset(NULL::%s, $1) WHERE domain_id = $2', target, target) USING rows, declared;
                GET DIAGNOSTICS part = ROW_COUNT;
                loaded := loaded + part;
            END LOOP;
        ELSE
            EXECUTE format('INSERT INTO %s OVERRIDING SYSTEM VALUE SELECT * FROM jsonb_populate_recordset(NULL::%s, $1)', target, target) USING rows;
            GET DIAGNOSTICS loaded = ROW_COUNT;
        END IF;

        IF table_name = 'public.person' THEN
            INSERT INTO public.restore_inactive SELECT id FROM public.person WHERE NOT active;
            UPDATE public.person SET active = true WHERE NOT active;
        END IF;

        IF table_name NOT IN ('public.derivation_label', 'public.derivation_label_revocation', 'public.person_credential') AND loaded > 0 THEN
            PERFORM pg_catalog.setval(pg_catalog.pg_get_serial_sequence(table_name, 'id')::regclass, (SELECT max((r ->> 'id')::bigint) FROM jsonb_array_elements(rows) r));
        END IF;
    EXCEPTION WHEN OTHERS THEN
        PERFORM public.restore_close(false);
        RETURN -1;
    END;

    RETURN loaded;
END
$load$;

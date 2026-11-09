-- The record of the submission (issue 75, CA-5): a submission opens a batch, so the batch holds what arrived: the identifier the system generated for it, how many bytes came, their SHA-256 and,
-- when the file came with a name that had a path in it, the code of that warning, never the name. Who submitted and when are the opened_by and opened_at the batch already has; why a submission
-- was turned back is the motive of the batch rejected. The columns are born with the batch, all or none (a batch opened by another way has none), and never change.
ALTER TABLE content.ingestion_batch
    ADD COLUMN submission text,
    ADD COLUMN received_bytes bigint,
    ADD COLUMN received_digest text,
    ADD COLUMN warning text,
    ADD CONSTRAINT ingestion_batch_submission_once UNIQUE (submission),
    ADD CONSTRAINT ingestion_batch_receipt CHECK (
        (submission IS NULL) = (received_bytes IS NULL) AND (submission IS NULL) = (received_digest IS NULL)
        AND (submission IS NULL OR (submission ~ '^[0-9a-f]{32}$' AND received_bytes >= 0 AND received_digest ~ '^[0-9a-f]{64}$'))
        AND (warning IS NULL OR (submission IS NOT NULL AND warning ~ '^[a-z][a-z0-9_]{0,62}$')));

GRANT INSERT (submission, received_bytes, received_digest, warning) ON content.ingestion_batch TO app;

-- What arrived never changes after the opening, by any statement and any session, the owner and a replica one included, as the condition never does (0038).
CREATE FUNCTION public.refuse_batch_receipt_change() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    IF NEW.submission IS DISTINCT FROM OLD.submission OR NEW.received_bytes IS DISTINCT FROM OLD.received_bytes
        OR NEW.received_digest IS DISTINCT FROM OLD.received_digest OR NEW.warning IS DISTINCT FROM OLD.warning THEN
        RAISE EXCEPTION 'what a submission brought never changes after the opening of its batch'
            USING ERRCODE = 'CE006', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    RETURN NEW;
END
$refuse$;

CREATE TRIGGER batch_receipt_is_never_changed BEFORE UPDATE ON content.ingestion_batch FOR EACH ROW EXECUTE FUNCTION public.refuse_batch_receipt_change();
ALTER TABLE content.ingestion_batch ENABLE ALWAYS TRIGGER batch_receipt_is_never_changed;

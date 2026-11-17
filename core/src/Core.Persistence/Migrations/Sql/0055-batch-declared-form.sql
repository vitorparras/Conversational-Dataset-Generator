-- The form a submission declares and the form its format carries (issue 821): each one of the four forms the pipeline knows, or none for a batch opened before the submission said them. Both
-- are written at the opening and never change after, whoever writes; the detail of the batch says the material came in the form of its format and was accommodated into the form declared.
ALTER TABLE content.ingestion_batch
    ADD COLUMN declared_form text,
    ADD COLUMN source_form text,
    ADD CONSTRAINT ingestion_batch_declared_form_listed CHECK (declared_form IS NULL OR declared_form IN ('conversation', 'comment', 'document', 'labeled_sequence')),
    ADD CONSTRAINT ingestion_batch_source_form_listed CHECK (source_form IS NULL OR source_form IN ('conversation', 'comment', 'document', 'labeled_sequence'));

GRANT INSERT (declared_form, source_form) ON content.ingestion_batch TO app;

-- The same function the 0054 left, with the declared form and the form of the format among what never changes.
CREATE OR REPLACE FUNCTION public.refuse_batch_change() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
DECLARE
    sampling boolean := OLD.review_sample IS NULL AND NEW.review_sample IS NOT NULL;
    reviewing boolean := OLD.reviewed_by IS NULL AND NEW.reviewed_by IS NOT NULL;
BEGIN
    IF OLD.state IS DISTINCT FROM 'open' OR NEW.state IS NULL OR NEW.state NOT IN ('open', 'accepted', 'rejected')
        OR NEW.id IS DISTINCT FROM OLD.id OR NEW.domain_id IS DISTINCT FROM OLD.domain_id
        OR NEW.opened_by IS DISTINCT FROM OLD.opened_by OR NEW.opened_at IS DISTINCT FROM OLD.opened_at
        OR NEW.declared_form IS DISTINCT FROM OLD.declared_form OR NEW.source_form IS DISTINCT FROM OLD.source_form
        OR (OLD.review_sample IS NOT NULL AND NEW.review_sample IS DISTINCT FROM OLD.review_sample)
        OR (OLD.reviewed_by IS NOT NULL AND NEW.reviewed_by IS DISTINCT FROM OLD.reviewed_by)
        OR NEW.sampled_at IS DISTINCT FROM OLD.sampled_at OR NEW.reviewed_at IS DISTINCT FROM OLD.reviewed_at
        OR (sampling AND (reviewing OR NEW.state <> 'open'))
        OR (reviewing AND OLD.review_sample IS NULL)
        OR (NEW.state = 'open' AND NOT (sampling OR reviewing))
        OR (NEW.state = 'open' AND NEW.review_finding IS NOT NULL)
        OR (reviewing AND NEW.state = 'accepted')
        OR (reviewing AND (NEW.state = 'rejected') <> (NEW.review_finding IS NOT NULL)) THEN
        RAISE EXCEPTION 'a batch passes from open to accepted or to rejected, once, and its sample is drawn and reviewed once; nothing else of it changes'
            USING ERRCODE = 'CE006', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    IF NEW.state = 'accepted' AND (OLD.reviewed_by IS NULL OR OLD.review_finding IS NOT NULL) THEN
        RAISE EXCEPTION 'a batch is accepted only after the review of its sample, and the review found nothing'
            USING ERRCODE = 'CE005', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    IF (sampling AND NOT public.batch_event_of_this_transaction(NEW.id, 'batch_sampled', 'administrative_act', NULL))
        OR (reviewing AND NOT public.batch_event_by(NEW.id, 'batch_reviewed', CASE WHEN NEW.review_finding IS NULL THEN 'review_clean' ELSE 'review_found' END, NEW.reviewed_by))
        OR (NEW.state <> 'open' AND NOT public.batch_event_of_this_transaction(NEW.id, 'batch_closed', CASE NEW.state WHEN 'accepted' THEN 'batch_accepted' ELSE 'batch_rejected' END, NEW.motive)) THEN
        RAISE EXCEPTION 'a batch changes only with the event of the change, with the same outcome and motive, in the same transaction'
            USING ERRCODE = 'CE005', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    IF sampling THEN
        NEW.sampled_at := pg_catalog.now();
    END IF;

    IF reviewing THEN
        NEW.reviewed_at := pg_catalog.now();
    END IF;

    IF NEW.state <> 'open' THEN
        NEW.closed_at := pg_catalog.now();
    END IF;

    RETURN NEW;
END
$refuse$;

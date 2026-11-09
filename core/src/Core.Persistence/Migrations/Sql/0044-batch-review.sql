-- The review of the sample (issue 83): a person reviews the sample the system drew, and one finding of personal data not treated rejects the whole batch. The review is a gate and not an estimate: there is
-- no rate to reach, and no path accepts a batch whose sample was not reviewed, the owner included. The chain learns the step batch_reviewed (by the reviewer, with the reason review_clean or review_found);
-- the checks change from the ones the 0043 left, and a finding is also the rejection of the batch, in the same transaction.
ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_step_listed,
    DROP CONSTRAINT custody_event_reason_listed,
    DROP CONSTRAINT custody_event_batch_shape,
    ADD CONSTRAINT custody_event_step_listed CHECK (step IN
        ('person_created', 'person_deactivated', 'person_function_changed', 'person_password_set', 'correction',
         'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'verify_chain', 'item_emptied', 'item_emptying_refused', 'epoch_rotated',
         'domain_created', 'seal_recorded', 'domain_label_rotated',
         'sign_in_accepted', 'sign_in_refused', 'credential_recomputed', 'key_loss_declared', 'recovery_tested', 'verify_guarantees',
         'owner_window_opened', 'owner_window_closed', 'restored', 'restore_rehearsed', 'domain_label_revoked', 'domain_rewrapped',
         'batch_opened', 'batch_closed', 'password_changed', 'configuration_submitted', 'occurrences_read', 'batch_sampled', 'batch_reviewed')),
    ADD CONSTRAINT custody_event_reason_listed CHECK (reason IN
        ('administrative_act', 'correction_of_record', 'content_read', 'verification_passed', 'verification_failed',
         'removal_requested_by_subject', 'contract_requirement', 'author_without_function', 'item_already_emptied',
         'credentials_accepted', 'password_wrong', 'account_not_recognized',
         'batch_accepted', 'batch_rejected', 'review_clean', 'review_found')),
    ADD CONSTRAINT custody_event_batch_shape CHECK (
        (step IN ('batch_opened', 'batch_closed', 'batch_sampled', 'batch_reviewed')) = (item_kind = 'ingestion_batch')
        AND (step = 'batch_closed') = (reason IN ('batch_accepted', 'batch_rejected'))
        AND (step = 'batch_reviewed') = (reason IN ('review_clean', 'review_found'))
        AND (step NOT IN ('batch_opened', 'batch_sampled') OR (reason = 'administrative_act' AND actor_person_id IS NOT NULL))
        AND (step <> 'batch_reviewed' OR actor_person_id IS NOT NULL)
        AND (step NOT IN ('batch_opened', 'batch_closed', 'batch_sampled', 'batch_reviewed') OR corrects IS NULL)
        AND (step <> 'batch_closed' OR (reason = 'batch_rejected') = (reference IS NOT NULL))
        AND (step <> 'batch_closed' OR reference IS NULL OR reference ~ '^[a-z][a-z0-9_]{0,62}$'));

-- Who reviewed and when, and the finding that rejected the batch: a name in snake_case, never free text. The moment is the server's, in the trigger below.
ALTER TABLE content.ingestion_batch
    ADD COLUMN reviewed_by bigint REFERENCES public.person (id),
    ADD COLUMN reviewed_at timestamptz,
    ADD COLUMN review_finding text,
    ADD CONSTRAINT ingestion_batch_review CHECK ((reviewed_by IS NULL) = (reviewed_at IS NULL) AND (reviewed_by IS NULL OR review_sample IS NOT NULL)),
    ADD CONSTRAINT ingestion_batch_finding CHECK (review_finding IS NULL OR (reviewed_by IS NOT NULL AND state = 'rejected' AND review_finding ~ '^[a-z][a-z0-9_]{0,62}$'));

GRANT UPDATE (reviewed_by, review_finding) ON content.ingestion_batch TO app;

-- Whether the event of the batch is in the chain, written by this very transaction and by this person.
CREATE FUNCTION public.batch_event_by(of_batch bigint, of_step text, of_reason text, of_actor bigint) RETURNS boolean
LANGUAGE sql
STABLE
RETURN EXISTS (
    SELECT 1 FROM public.custody_event e
    WHERE e.item_kind = 'ingestion_batch' AND e.item_id = of_batch AND e.step = of_step AND e.reason = of_reason AND e.actor_person_id = of_actor
      AND e.xmin = pg_catalog.pg_current_xact_id()::pg_catalog.xid
      AND e.occurred_at >= pg_catalog.transaction_timestamp());

-- A batch changes in three ways: the draw of its sample, the review of it (once, while open) and the closing. A batch is accepted only with a review that found nothing, and a finding is a rejection:
-- the batch, its items and its maps go in the very transaction that records the review. Each change needs its event in this transaction; the moments are the server's.
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

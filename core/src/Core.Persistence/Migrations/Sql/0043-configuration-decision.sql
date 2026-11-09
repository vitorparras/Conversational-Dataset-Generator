-- The decision on a change of configuration (issue 634, part 3; decision 0053): another active person with the technical administration approves or refuses a pending submission. The decision is
-- an event of the chain and nothing else: the item is the submission, the author is who decided and the reference is the sequence of the event of the submission; the file stays as it is.
-- The server refuses a decision by the person who submitted, one that points at another event, a second one on the same submission and any after the longest deadline the configuration
-- takes (168 h); the deadline in force is the core's to check. Only the checks change, from the last ones the earlier steps left (the steps from the 0042, with origin_read, the shape of the submission
-- from the 0041 and the shape of the reference from the 0037): everything they list and every clause they fix stays, and the decision comes in beside it.
ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_step_listed,
    DROP CONSTRAINT custody_event_configuration_shape,
    DROP CONSTRAINT custody_event_emptying_shape,
    ADD CONSTRAINT custody_event_step_listed CHECK (step IN
        ('person_created', 'person_deactivated', 'person_function_changed', 'person_password_set', 'correction',
         'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'verify_chain', 'item_emptied', 'item_emptying_refused', 'epoch_rotated',
         'domain_created', 'seal_recorded', 'domain_label_rotated',
         'sign_in_accepted', 'sign_in_refused', 'credential_recomputed', 'key_loss_declared', 'recovery_tested', 'verify_guarantees',
         'owner_window_opened', 'owner_window_closed', 'restored', 'restore_rehearsed', 'domain_label_revoked', 'domain_rewrapped',
         'batch_opened', 'batch_closed', 'password_changed', 'configuration_submitted', 'origin_read', 'configuration_approved', 'configuration_refused')),
    ADD CONSTRAINT custody_event_configuration_shape CHECK (
        (step IN ('configuration_submitted', 'configuration_approved', 'configuration_refused')) = (item_kind = 'configuration_change')
        AND (item_kind <> 'configuration_change' OR (reason = 'administrative_act' AND actor_person_id IS NOT NULL AND corrects IS NULL))
        AND (step <> 'configuration_submitted' OR reference IS NULL)
        AND (step NOT IN ('configuration_approved', 'configuration_refused') OR coalesce(reference ~ '^[1-9][0-9]{0,18}$', false))),
    ADD CONSTRAINT custody_event_emptying_shape CHECK (
        (step IN ('recovery_tested', 'verify_chain')
            OR (reference IS NOT NULL)
                = (step IN ('item_emptied', 'item_emptying_refused', 'domain_label_rotated', 'key_loss_declared', 'owner_window_opened', 'owner_window_closed', 'restored',
                            'restore_rehearsed', 'domain_label_revoked', 'password_changed', 'configuration_approved', 'configuration_refused') OR reason = 'batch_rejected'))
        AND (step <> 'verify_chain' OR reference IS NULL OR reference ~ '^requested-by/[1-9][0-9]{0,17}$')
        AND (reference IS NULL OR step IN ('owner_window_opened', 'owner_window_closed', 'restored') OR reference ~ '^[A-Za-z0-9._/-]{1,64}$')
        AND (step NOT IN ('owner_window_opened', 'owner_window_closed', 'restored') OR reference ~ '^[a-z0-9=;]{1,160}$')
        AND (step <> 'restore_rehearsed' OR reference ~ '^[a-z]+(\.[a-z]+)*$')
        AND (step NOT IN ('item_emptied', 'item_emptying_refused') OR item_kind = 'item_text')
        AND (step = 'item_emptied') = (reason IN ('removal_requested_by_subject', 'contract_requirement'))
        AND (step = 'item_emptying_refused') = (reason IN ('author_without_function', 'item_already_emptied'))
        AND (step <> 'item_emptied' OR actor_person_id IS NOT NULL));

-- A submission is decided once: a second decision, or two at the same time, find the first one here.
CREATE UNIQUE INDEX custody_event_configuration_decided_once ON public.custody_event (item_id) WHERE step IN ('configuration_approved', 'configuration_refused');

-- The decision points at the event of its submission, is by another person and comes before the longest deadline; the load of a restore puts back what was decided, as it was.
CREATE FUNCTION public.refuse_configuration_decision_out_of_turn() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
DECLARE
    submission record;
BEGIN
    IF public.restore_window_is_open() THEN
        RETURN NEW;
    END IF;

    SELECT e.sequence, c.submitted_by, c.submitted_at INTO submission
    FROM public.custody_event e JOIN public.configuration_change c ON c.id = e.item_id
    WHERE e.item_kind = 'configuration_change' AND e.item_id = NEW.item_id AND e.step = 'configuration_submitted';
    IF NOT FOUND OR submission.sequence::text <> NEW.reference THEN
        RAISE EXCEPTION 'a decision on a change of configuration points at the event of its submission'
            USING ERRCODE = 'check_violation', CONSTRAINT = 'configuration_decision_points_at_submission';
    ELSIF submission.submitted_by = NEW.actor_person_id THEN
        RAISE EXCEPTION 'a change of configuration is decided by a person other than the one who submitted it'
            USING ERRCODE = 'check_violation', CONSTRAINT = 'configuration_decision_by_another';
    ELSIF submission.submitted_at < pg_catalog.clock_timestamp() - interval '168 hours' THEN
        RAISE EXCEPTION 'a change of configuration is decided before the longest deadline'
            USING ERRCODE = 'check_violation', CONSTRAINT = 'configuration_decision_in_time';
    END IF;

    RETURN NEW;
END
$refuse$;

CREATE TRIGGER custody_event_configuration_decision BEFORE INSERT ON public.custody_event FOR EACH ROW
    WHEN (NEW.step IN ('configuration_approved', 'configuration_refused')) EXECUTE FUNCTION public.refuse_configuration_decision_out_of_turn();
ALTER TABLE public.custody_event ENABLE ALWAYS TRIGGER custody_event_configuration_decision;

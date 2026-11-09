-- PROVISIONAL NUMBER: the definitive one is taken when the pull request opens, after the decision on a change of configuration (0044) and the 0045 of issue 635.
-- A change of configuration in force (issue 634, part 4; decision 0053): when the core starts with the value an approved change asked for, it writes one event, by the system, that points
-- at the event of the approval. Only the checks change, from the last ones the 0044 of the decision left (with origin_read): everything they list and every clause they fix stays, and the new step comes in beside it.
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
         'batch_opened', 'batch_closed', 'password_changed', 'configuration_submitted', 'origin_read', 'configuration_approved', 'configuration_refused',
         'configuration_in_force')),
    ADD CONSTRAINT custody_event_configuration_shape CHECK (
        (step IN ('configuration_submitted', 'configuration_approved', 'configuration_refused', 'configuration_in_force')) = (item_kind = 'configuration_change')
        AND (item_kind <> 'configuration_change' OR (reason = 'administrative_act' AND corrects IS NULL AND (actor_person_id IS NOT NULL) = (step <> 'configuration_in_force')))
        AND (step <> 'configuration_submitted' OR reference IS NULL)
        AND (step NOT IN ('configuration_approved', 'configuration_refused', 'configuration_in_force') OR coalesce(reference ~ '^[1-9][0-9]{0,18}$', false))),
    ADD CONSTRAINT custody_event_emptying_shape CHECK (
        (step IN ('recovery_tested', 'verify_chain')
            OR (reference IS NOT NULL)
                = (step IN ('item_emptied', 'item_emptying_refused', 'domain_label_rotated', 'key_loss_declared', 'owner_window_opened', 'owner_window_closed', 'restored',
                            'restore_rehearsed', 'domain_label_revoked', 'password_changed', 'configuration_approved', 'configuration_refused', 'configuration_in_force')
                    OR reason = 'batch_rejected'))
        AND (step <> 'verify_chain' OR reference IS NULL OR reference ~ '^requested-by/[1-9][0-9]{0,17}$')
        AND (reference IS NULL OR step IN ('owner_window_opened', 'owner_window_closed', 'restored') OR reference ~ '^[A-Za-z0-9._/-]{1,64}$')
        AND (step NOT IN ('owner_window_opened', 'owner_window_closed', 'restored') OR reference ~ '^[a-z0-9=;]{1,160}$')
        AND (step <> 'restore_rehearsed' OR reference ~ '^[a-z]+(\.[a-z]+)*$')
        AND (step NOT IN ('item_emptied', 'item_emptying_refused') OR item_kind = 'item_text')
        AND (step = 'item_emptied') = (reason IN ('removal_requested_by_subject', 'contract_requirement'))
        AND (step = 'item_emptying_refused') = (reason IN ('author_without_function', 'item_already_emptied'))
        AND (step <> 'item_emptied' OR actor_person_id IS NOT NULL));

-- A change is in force once.
CREATE UNIQUE INDEX custody_event_configuration_in_force_once ON public.custody_event (item_id) WHERE step = 'configuration_in_force';

-- The event points at the event of the approval of the same change; the load of a restore puts back what was recorded, as it was.
CREATE FUNCTION public.refuse_configuration_in_force_unapproved() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    IF public.restore_window_is_open() THEN
        RETURN NEW;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM public.custody_event e
                   WHERE e.item_kind = 'configuration_change' AND e.item_id = NEW.item_id AND e.step = 'configuration_approved' AND e.sequence::text = NEW.reference) THEN
        RAISE EXCEPTION 'a change of configuration is in force only after it was approved, and the event points at the approval'
            USING ERRCODE = 'check_violation', CONSTRAINT = 'configuration_in_force_points_at_approval';
    END IF;

    RETURN NEW;
END
$refuse$;

CREATE TRIGGER custody_event_configuration_in_force BEFORE INSERT ON public.custody_event FOR EACH ROW
    WHEN (NEW.step = 'configuration_in_force') EXECUTE FUNCTION public.refuse_configuration_in_force_unapproved();
ALTER TABLE public.custody_event ENABLE ALWAYS TRIGGER custody_event_configuration_in_force;

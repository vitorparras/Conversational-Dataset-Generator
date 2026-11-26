-- The revelation of a marker (issue 105, fase 5 fatia D). One row per authorization is its single use: the authorization and when, written with its event in the same transaction, and
-- nothing changes after. No column holds the value revealed: the application opens the map only after the event is committed, hands the identity over once and keeps nothing of it.
SELECT public.create_content_table('reversal_revelation',
    $columns$authorization_id bigint NOT NULL,
     revealed_at timestamptz NOT NULL DEFAULT pg_catalog.now(),
     CONSTRAINT reversal_revelation_authorization FOREIGN KEY (authorization_id, domain_id) REFERENCES content.reversal_authorization (id, domain_id),
     CONSTRAINT reversal_revelation_once UNIQUE (authorization_id)$columns$);

COMMENT ON TABLE content.reversal_revelation IS 'that a marker was revealed, by which authorization and when; never what it was';

GRANT SELECT, INSERT ON content.reversal_revelation TO PUBLIC;

-- An authorization serves only while it holds. The application judges the same in its transaction and records the denial; this holds for whoever does not.
CREATE FUNCTION public.refuse_reversal_revelation() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    -- A row of a domain other than the declared one, or with none declared, is the row policy's to refuse, with its own refusal.
    IF public.declared_domain() IS NULL OR NEW.domain_id IS DISTINCT FROM public.declared_domain() THEN
        RETURN NEW;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM content.reversal_authorization a WHERE a.id = NEW.authorization_id AND a.domain_id = NEW.domain_id AND a.valid_until > pg_catalog.now()) THEN
        RAISE EXCEPTION 'a marker is revealed only by an authorization that holds'
            USING ERRCODE = 'CE007', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    RETURN NEW;
END
$refuse$;

CREATE TRIGGER reversal_revelation_never_changes BEFORE UPDATE OR DELETE ON content.reversal_revelation FOR EACH ROW EXECUTE FUNCTION public.refuse_reversal_change();
CREATE TRIGGER reversal_revelations_are_never_emptied BEFORE TRUNCATE ON content.reversal_revelation FOR EACH STATEMENT EXECUTE FUNCTION public.refuse_reversal_change();
CREATE TRIGGER reversal_revelation_is_bounded BEFORE INSERT ON content.reversal_revelation FOR EACH ROW EXECUTE FUNCTION public.refuse_reversal_revelation();
CREATE CONSTRAINT TRIGGER reversal_revelation_needs_event AFTER INSERT ON content.reversal_revelation DEFERRABLE INITIALLY DEFERRED
    FOR EACH ROW EXECUTE FUNCTION public.refuse_reversal_without_event('reversal_revelation', 'reversal_revealed');
ALTER TABLE content.reversal_revelation ENABLE ALWAYS TRIGGER reversal_revelation_never_changes;
ALTER TABLE content.reversal_revelation ENABLE ALWAYS TRIGGER reversal_revelations_are_never_emptied;
ALTER TABLE content.reversal_revelation ENABLE ALWAYS TRIGGER reversal_revelation_is_bounded;
ALTER TABLE content.reversal_revelation ENABLE ALWAYS TRIGGER reversal_revelation_needs_event;

-- The chain learns the revelation and its denial (eventos-da-reversao.md): the revelation points to its row with the authorization as the reference, and the denial to the authorization,
-- with why and no reference, both by the person who asked. The lists and the shapes are the ones of the reversal-request (202611251200) with the new names.
ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_item_kind_listed,
    DROP CONSTRAINT custody_event_step_listed,
    DROP CONSTRAINT custody_event_reason_listed,
    DROP CONSTRAINT custody_event_emptying_shape,
    DROP CONSTRAINT custody_event_reversal_shape,
    ADD CONSTRAINT custody_event_item_kind_listed CHECK (item_kind IN
        ('person', 'item_text', 'reversal_map', 'custody_chain', 'content_epoch', 'domain', 'sign_in_attempt', 'master_key', 'ingestion_batch', 'configuration_change',
         'sanitization_rerun', 'reversal_request', 'reversal_authorization', 'reversal_revelation')),
    ADD CONSTRAINT custody_event_step_listed CHECK (step IN
        ('person_created', 'person_deactivated', 'person_function_changed', 'person_password_set', 'correction',
         'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'verify_chain', 'item_emptied', 'item_emptying_refused', 'epoch_rotated',
         'domain_created', 'seal_recorded', 'domain_label_rotated',
         'sign_in_accepted', 'sign_in_refused', 'credential_recomputed', 'key_loss_declared', 'recovery_tested', 'verify_guarantees',
         'owner_window_opened', 'owner_window_closed', 'restored', 'restore_rehearsed', 'domain_label_revoked', 'domain_rewrapped',
         'batch_opened', 'batch_closed', 'password_changed', 'configuration_submitted', 'origin_read', 'configuration_approved', 'configuration_refused',
         'configuration_in_force', 'person_email_corrected', 'configuration_not_applied', 'occurrences_read', 'batch_sampled', 'batch_reviewed', 'batch_sanitized',
         'sanitization_rerun', 'item_resanitized', 'batch_pseudonymized',
         'reversal_requested', 'reversal_request_refused', 'reversal_request_read', 'reversal_authorized', 'reversal_authorization_refused', 'reversal_declined',
         'reversal_revealed', 'reversal_denied')),
    ADD CONSTRAINT custody_event_reason_listed CHECK (reason IN
        ('administrative_act', 'correction_of_record', 'content_read', 'verification_passed', 'verification_failed',
         'removal_requested_by_subject', 'contract_requirement', 'author_without_function', 'item_already_emptied',
         'credentials_accepted', 'password_wrong', 'account_not_recognized',
         'batch_accepted', 'batch_rejected', 'review_clean', 'review_found',
         'sanitization_identical', 'sanitization_replaced', 'sanitization_refused', 'sanitization_superseded',
         'reversal_function', 'reversal_scope', 'reversal_self', 'reversal_expired', 'reversal_used', 'reversal_emptied')),
    ADD CONSTRAINT custody_event_emptying_shape CHECK (
        (step IN ('recovery_tested', 'verify_chain')
            OR (reference IS NOT NULL)
                = (step IN ('item_emptied', 'item_emptying_refused', 'domain_label_rotated', 'key_loss_declared', 'owner_window_opened', 'owner_window_closed', 'restored',
                            'restore_rehearsed', 'domain_label_revoked', 'password_changed', 'configuration_approved', 'configuration_refused', 'configuration_in_force',
                            'configuration_not_applied', 'batch_sanitized', 'sanitization_rerun', 'item_resanitized',
                            'reversal_requested', 'reversal_request_refused', 'reversal_authorized', 'reversal_revealed')
                    OR reason = 'batch_rejected'))
        AND (step <> 'verify_chain' OR reference IS NULL OR reference ~ '^requested-by/[1-9][0-9]{0,17}$')
        AND (reference IS NULL OR step IN ('owner_window_opened', 'owner_window_closed', 'restored') OR reference ~ '^[A-Za-z0-9._/-]{1,64}$')
        AND (step NOT IN ('owner_window_opened', 'owner_window_closed', 'restored') OR reference ~ '^[a-z0-9=;]{1,160}$')
        AND (step <> 'restore_rehearsed' OR reference ~ '^[a-z]+(\.[a-z]+)*$')
        AND (step NOT IN ('item_emptied', 'item_emptying_refused') OR item_kind = 'item_text')
        AND (step = 'item_emptied') = (reason IN ('removal_requested_by_subject', 'contract_requirement'))
        AND (step = 'item_emptying_refused') = (reason IN ('author_without_function', 'item_already_emptied'))
        AND (step <> 'item_emptied' OR actor_person_id IS NOT NULL)),
    ADD CONSTRAINT custody_event_reversal_shape CHECK (
        (item_kind NOT IN ('reversal_request', 'reversal_authorization', 'reversal_revelation')
            OR step IN ('reversal_requested', 'reversal_request_read', 'reversal_authorized', 'reversal_authorization_refused', 'reversal_declined', 'reversal_revealed',
                        'reversal_denied', 'correction'))
        AND (reason NOT IN ('reversal_function', 'reversal_scope', 'reversal_self', 'reversal_expired', 'reversal_used', 'reversal_emptied')
            OR step IN ('reversal_request_refused', 'reversal_authorization_refused', 'reversal_denied'))
        AND (step NOT IN ('reversal_requested', 'reversal_request_refused', 'reversal_request_read', 'reversal_authorized', 'reversal_authorization_refused', 'reversal_declined',
                          'reversal_revealed', 'reversal_denied')
            OR (actor_person_id IS NOT NULL AND corrects IS NULL))
        AND (step <> 'reversal_requested'
            OR (item_kind = 'reversal_request' AND reason = 'administrative_act' AND reference ~ '^(PESSOA|LUGAR|ORGANIZACAO)_[0-9]{8}$'))
        AND (step <> 'reversal_request_refused'
            OR (item_kind = 'item_text' AND reason IN ('reversal_function', 'reversal_scope') AND reference ~ '^(PESSOA|LUGAR|ORGANIZACAO)_[0-9]{8}$')
            OR (item_kind = 'domain' AND reason = 'reversal_scope' AND reference IN ('dominio', 'varios-itens', 'varios-marcadores')))
        AND (step NOT IN ('reversal_request_read', 'reversal_declined') OR (item_kind = 'reversal_request' AND reason = 'administrative_act' AND reference IS NULL))
        AND (step <> 'reversal_authorized' OR (item_kind = 'reversal_authorization' AND reason = 'administrative_act' AND reference ~ '^[1-9][0-9]{0,17}$'))
        AND (step <> 'reversal_authorization_refused' OR (item_kind = 'reversal_request' AND reason IN ('reversal_self', 'reversal_function') AND reference IS NULL))
        AND (step <> 'reversal_revealed' OR (item_kind = 'reversal_revelation' AND reason = 'administrative_act' AND reference ~ '^[1-9][0-9]{0,17}$'))
        AND (step <> 'reversal_denied'
            OR (item_kind = 'reversal_authorization' AND reason IN ('reversal_expired', 'reversal_used', 'reversal_emptied') AND reference IS NULL)));

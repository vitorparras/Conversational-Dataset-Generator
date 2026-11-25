-- The request to reverse a marker and its authorization (issues 103 and 104, fase 5 fatia C). Each act is a row of its own with its event in the same transaction, and nothing changes
-- after: the state of a request is read from its rows and its events. Why the identity is asked for and what will be done with it go sealed in the row, with the purpose of the map and a
-- key of the row, as the shift of the dates is (202611231200); the event never carries them.

-- The request: one marker in one item, who asked and when, and for how many days. The item is named without a key to its row: an item whose batch is rejected goes, and its request,
-- which then reverses nothing, stays as the record of what was asked.
SELECT public.create_content_table('reversal_request',
    $columns$item_id bigint NOT NULL,
     marker text NOT NULL,
     requested_by bigint NOT NULL REFERENCES public.person (id),
     requested_at timestamptz NOT NULL DEFAULT pg_catalog.now(),
     term_days integer NOT NULL,
     justification bytea NOT NULL,
     dek_epoch integer NOT NULL,
     dek_wrapped bytea NOT NULL,
     CONSTRAINT reversal_request_marker CHECK (marker ~ '^(PESSOA|LUGAR|ORGANIZACAO)_[0-9]{8}$'),
     CONSTRAINT reversal_request_term CHECK (term_days > 0),
     CONSTRAINT reversal_request_key CHECK (dek_epoch > 0),
     CONSTRAINT reversal_request_id_domain UNIQUE (id, domain_id)$columns$);

COMMENT ON COLUMN content.reversal_request.justification IS 'protected:mapa-reversao';
COMMENT ON COLUMN content.reversal_request.dek_wrapped IS 'protected:mapa-reversao';

-- The authorization of a request: who authorized, when, and until when. A renewal is another row, once the one before expired.
SELECT public.create_content_table('reversal_authorization',
    $columns$request_id bigint NOT NULL,
     authorized_by bigint NOT NULL REFERENCES public.person (id),
     authorized_at timestamptz NOT NULL DEFAULT pg_catalog.now(),
     valid_until timestamptz NOT NULL,
     CONSTRAINT reversal_authorization_request FOREIGN KEY (request_id, domain_id) REFERENCES content.reversal_request (id, domain_id),
     CONSTRAINT reversal_authorization_id_domain UNIQUE (id, domain_id)$columns$);

CREATE INDEX reversal_authorization_of_request ON content.reversal_authorization (request_id);

GRANT SELECT, INSERT ON content.reversal_request, content.reversal_authorization TO PUBLIC;
GRANT UPDATE (dek_epoch, dek_wrapped) ON content.reversal_request TO app;

-- A request and an authorization are what was asked and what was decided: no row of either goes, and an authorization never changes.
CREATE FUNCTION public.refuse_reversal_change() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    RAISE EXCEPTION 'a request to reverse a marker and its authorization never change and never go'
        USING ERRCODE = 'CE006', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
END
$refuse$;

-- What a request asks never changes: only the key that wraps its text is wrapped again, and only with the event of the rotation or of the rewrap in the same transaction, as for the shift.
CREATE FUNCTION public.refuse_reversal_request_change() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    IF (NEW.id, NEW.domain_id, NEW.item_id, NEW.marker, NEW.requested_by, NEW.requested_at, NEW.term_days, NEW.justification)
        IS DISTINCT FROM (OLD.id, OLD.domain_id, OLD.item_id, OLD.marker, OLD.requested_by, OLD.requested_at, OLD.term_days, OLD.justification) THEN
        RAISE EXCEPTION 'a request to reverse a marker never changes: only the key that wraps its text is wrapped again'
            USING ERRCODE = 'CE006', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    IF NOT (public.custody_event_of_this_transaction('content_epoch', NEW.dek_epoch, 'epoch_rotated')
        OR (NEW.dek_epoch = OLD.dek_epoch AND public.custody_event_of_this_transaction('domain', NEW.domain_id, 'domain_rewrapped'))) THEN
        RAISE EXCEPTION 'a key is wrapped again only with the event of the rotation or of the rewrap in the same transaction'
            USING ERRCODE = 'CE005', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    RETURN NEW;
END
$refuse$;

-- A row is written only with its own event in the same transaction, checked at the commit, since the event names the row the insert numbered.
CREATE FUNCTION public.refuse_reversal_without_event() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    IF NOT public.custody_event_of_this_transaction(TG_ARGV[0], NEW.id, TG_ARGV[1]) THEN
        RAISE EXCEPTION 'a request to reverse a marker and its authorization are written only with their event in the same transaction'
            USING ERRCODE = 'CE005', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    RETURN NULL;
END
$refuse$;

-- Who asked never authorizes, a term that is not to come authorizes nothing, a request declined is not authorized, and a second authorization waits for the one in force to expire.
-- The application judges the same before it writes; this holds for whoever does not, and the serializable transaction of the chain makes two authorizations at once collide here.
CREATE FUNCTION public.refuse_reversal_authorization() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    -- A row of a domain other than the declared one, or with none declared, is the row policy's to refuse, with its own refusal.
    IF public.declared_domain() IS NULL OR NEW.domain_id IS DISTINCT FROM public.declared_domain() THEN
        RETURN NEW;
    END IF;

    IF NEW.valid_until <= pg_catalog.now() THEN
        RAISE EXCEPTION 'an authorization holds until a moment to come'
            USING ERRCODE = 'CE007', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    IF EXISTS (SELECT 1 FROM content.reversal_request r WHERE r.id = NEW.request_id AND r.domain_id = NEW.domain_id AND r.requested_by = NEW.authorized_by) THEN
        RAISE EXCEPTION 'who asked for a reversal does not authorize it'
            USING ERRCODE = 'CE007', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    IF EXISTS (SELECT 1 FROM content.reversal_authorization a WHERE a.request_id = NEW.request_id AND a.domain_id = NEW.domain_id AND a.valid_until > pg_catalog.now())
        OR EXISTS (SELECT 1 FROM public.custody_event e WHERE e.item_kind = 'reversal_request' AND e.item_id = NEW.request_id AND e.step = 'reversal_declined') THEN
        RAISE EXCEPTION 'a request is authorized once the authorization before expired, and never once declined'
            USING ERRCODE = 'CE007', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    RETURN NEW;
END
$refuse$;

CREATE TRIGGER reversal_request_change_is_bounded BEFORE UPDATE ON content.reversal_request FOR EACH ROW EXECUTE FUNCTION public.refuse_reversal_request_change();
CREATE TRIGGER reversal_request_is_never_removed BEFORE DELETE ON content.reversal_request FOR EACH ROW EXECUTE FUNCTION public.refuse_reversal_change();
CREATE TRIGGER reversal_requests_are_never_emptied BEFORE TRUNCATE ON content.reversal_request FOR EACH STATEMENT EXECUTE FUNCTION public.refuse_reversal_change();
CREATE CONSTRAINT TRIGGER reversal_request_needs_event AFTER INSERT ON content.reversal_request DEFERRABLE INITIALLY DEFERRED
    FOR EACH ROW EXECUTE FUNCTION public.refuse_reversal_without_event('reversal_request', 'reversal_requested');
CREATE TRIGGER reversal_authorization_never_changes BEFORE UPDATE OR DELETE ON content.reversal_authorization FOR EACH ROW EXECUTE FUNCTION public.refuse_reversal_change();
CREATE TRIGGER reversal_authorizations_are_never_emptied BEFORE TRUNCATE ON content.reversal_authorization FOR EACH STATEMENT EXECUTE FUNCTION public.refuse_reversal_change();
CREATE TRIGGER reversal_authorization_is_bounded BEFORE INSERT ON content.reversal_authorization FOR EACH ROW EXECUTE FUNCTION public.refuse_reversal_authorization();
CREATE CONSTRAINT TRIGGER reversal_authorization_needs_event AFTER INSERT ON content.reversal_authorization DEFERRABLE INITIALLY DEFERRED
    FOR EACH ROW EXECUTE FUNCTION public.refuse_reversal_without_event('reversal_authorization', 'reversal_authorized');
ALTER TABLE content.reversal_request ENABLE ALWAYS TRIGGER reversal_request_change_is_bounded;
ALTER TABLE content.reversal_request ENABLE ALWAYS TRIGGER reversal_request_is_never_removed;
ALTER TABLE content.reversal_request ENABLE ALWAYS TRIGGER reversal_requests_are_never_emptied;
ALTER TABLE content.reversal_request ENABLE ALWAYS TRIGGER reversal_request_needs_event;
ALTER TABLE content.reversal_authorization ENABLE ALWAYS TRIGGER reversal_authorization_never_changes;
ALTER TABLE content.reversal_authorization ENABLE ALWAYS TRIGGER reversal_authorizations_are_never_emptied;
ALTER TABLE content.reversal_authorization ENABLE ALWAYS TRIGGER reversal_authorization_is_bounded;
ALTER TABLE content.reversal_authorization ENABLE ALWAYS TRIGGER reversal_authorization_needs_event;

-- The chain learns the steps of the reversal and what each points to (eventos-da-reversao.md): the request, its refusal, the reading of its text, its authorization, the refusal of
-- it and the decline, by the person who acted. The lists are the ones the sanitization-rerun and the pseudonymization left, with the new names; the shape of the emptying is the one of the
-- sanitization-rerun with the steps that carry a reference beside the others; the shape of a domain is the one of the date-shift, and leaves the domain to the refusal by the scope.
ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_item_kind_listed,
    DROP CONSTRAINT custody_event_step_listed,
    DROP CONSTRAINT custody_event_reason_listed,
    DROP CONSTRAINT custody_event_emptying_shape,
    DROP CONSTRAINT custody_event_domain_shape,
    ADD CONSTRAINT custody_event_item_kind_listed CHECK (item_kind IN
        ('person', 'item_text', 'reversal_map', 'custody_chain', 'content_epoch', 'domain', 'sign_in_attempt', 'master_key', 'ingestion_batch', 'configuration_change',
         'sanitization_rerun', 'reversal_request', 'reversal_authorization')),
    ADD CONSTRAINT custody_event_step_listed CHECK (step IN
        ('person_created', 'person_deactivated', 'person_function_changed', 'person_password_set', 'correction',
         'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'verify_chain', 'item_emptied', 'item_emptying_refused', 'epoch_rotated',
         'domain_created', 'seal_recorded', 'domain_label_rotated',
         'sign_in_accepted', 'sign_in_refused', 'credential_recomputed', 'key_loss_declared', 'recovery_tested', 'verify_guarantees',
         'owner_window_opened', 'owner_window_closed', 'restored', 'restore_rehearsed', 'domain_label_revoked', 'domain_rewrapped',
         'batch_opened', 'batch_closed', 'password_changed', 'configuration_submitted', 'origin_read', 'configuration_approved', 'configuration_refused',
         'configuration_in_force', 'person_email_corrected', 'configuration_not_applied', 'occurrences_read', 'batch_sampled', 'batch_reviewed', 'batch_sanitized',
         'sanitization_rerun', 'item_resanitized', 'batch_pseudonymized',
         'reversal_requested', 'reversal_request_refused', 'reversal_request_read', 'reversal_authorized', 'reversal_authorization_refused', 'reversal_declined')),
    ADD CONSTRAINT custody_event_reason_listed CHECK (reason IN
        ('administrative_act', 'correction_of_record', 'content_read', 'verification_passed', 'verification_failed',
         'removal_requested_by_subject', 'contract_requirement', 'author_without_function', 'item_already_emptied',
         'credentials_accepted', 'password_wrong', 'account_not_recognized',
         'batch_accepted', 'batch_rejected', 'review_clean', 'review_found',
         'sanitization_identical', 'sanitization_replaced', 'sanitization_refused', 'sanitization_superseded',
         'reversal_function', 'reversal_scope', 'reversal_self')),
    ADD CONSTRAINT custody_event_emptying_shape CHECK (
        (step IN ('recovery_tested', 'verify_chain')
            OR (reference IS NOT NULL)
                = (step IN ('item_emptied', 'item_emptying_refused', 'domain_label_rotated', 'key_loss_declared', 'owner_window_opened', 'owner_window_closed', 'restored',
                            'restore_rehearsed', 'domain_label_revoked', 'password_changed', 'configuration_approved', 'configuration_refused', 'configuration_in_force',
                            'configuration_not_applied', 'batch_sanitized', 'sanitization_rerun', 'item_resanitized',
                            'reversal_requested', 'reversal_request_refused', 'reversal_authorized')
                    OR reason = 'batch_rejected'))
        AND (step <> 'verify_chain' OR reference IS NULL OR reference ~ '^requested-by/[1-9][0-9]{0,17}$')
        AND (reference IS NULL OR step IN ('owner_window_opened', 'owner_window_closed', 'restored') OR reference ~ '^[A-Za-z0-9._/-]{1,64}$')
        AND (step NOT IN ('owner_window_opened', 'owner_window_closed', 'restored') OR reference ~ '^[a-z0-9=;]{1,160}$')
        AND (step <> 'restore_rehearsed' OR reference ~ '^[a-z]+(\.[a-z]+)*$')
        AND (step NOT IN ('item_emptied', 'item_emptying_refused') OR item_kind = 'item_text')
        AND (step = 'item_emptied') = (reason IN ('removal_requested_by_subject', 'contract_requirement'))
        AND (step = 'item_emptying_refused') = (reason IN ('author_without_function', 'item_already_emptied'))
        AND (step <> 'item_emptied' OR actor_person_id IS NOT NULL)),
    ADD CONSTRAINT custody_event_domain_shape CHECK (
        (step IN ('domain_created', 'domain_label_rotated', 'domain_label_revoked', 'domain_rewrapped')) = (item_kind = 'domain' AND step <> 'reversal_request_refused')
        AND (step NOT IN ('domain_created', 'domain_label_rotated', 'domain_label_revoked', 'domain_rewrapped')
            OR (actor_system AND reason = 'administrative_act' AND corrects IS NULL))
        AND (step NOT IN ('domain_created', 'domain_rewrapped') OR reference IS NULL)
        AND (step <> 'domain_label_rotated' OR coalesce(reference ~ '^v[1-9][0-9]*$', false))
        AND (step <> 'domain_label_revoked' OR coalesce(reference ~ '^(texto-bruto|mapa-reversao|deslocamento-datas)/[1-9][0-9]*/v[1-9][0-9]*$', false))
        AND (step <> 'domain_label_revoked' OR split_part(reference, '/', 2) = item_id::text)),
    ADD CONSTRAINT custody_event_reversal_shape CHECK (
        (item_kind NOT IN ('reversal_request', 'reversal_authorization')
            OR step IN ('reversal_requested', 'reversal_request_read', 'reversal_authorized', 'reversal_authorization_refused', 'reversal_declined', 'correction'))
        AND (reason NOT IN ('reversal_function', 'reversal_scope', 'reversal_self') OR step IN ('reversal_request_refused', 'reversal_authorization_refused'))
        AND (step NOT IN ('reversal_requested', 'reversal_request_refused', 'reversal_request_read', 'reversal_authorized', 'reversal_authorization_refused', 'reversal_declined')
            OR (actor_person_id IS NOT NULL AND corrects IS NULL))
        AND (step <> 'reversal_requested'
            OR (item_kind = 'reversal_request' AND reason = 'administrative_act' AND reference ~ '^(PESSOA|LUGAR|ORGANIZACAO)_[0-9]{8}$'))
        AND (step <> 'reversal_request_refused'
            OR (item_kind = 'item_text' AND reason IN ('reversal_function', 'reversal_scope') AND reference ~ '^(PESSOA|LUGAR|ORGANIZACAO)_[0-9]{8}$')
            OR (item_kind = 'domain' AND reason = 'reversal_scope' AND reference IN ('dominio', 'varios-itens', 'varios-marcadores')))
        AND (step NOT IN ('reversal_request_read', 'reversal_declined') OR (item_kind = 'reversal_request' AND reason = 'administrative_act' AND reference IS NULL))
        AND (step <> 'reversal_authorized' OR (item_kind = 'reversal_authorization' AND reason = 'administrative_act' AND reference ~ '^[1-9][0-9]{0,17}$'))
        AND (step <> 'reversal_authorization_refused' OR (item_kind = 'reversal_request' AND reason IN ('reversal_self', 'reversal_function') AND reference IS NULL)));

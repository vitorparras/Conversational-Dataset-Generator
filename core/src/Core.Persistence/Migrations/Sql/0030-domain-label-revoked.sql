-- The revocation of a label of a domain (issue 56, part 1b): a label revoked encrypts nothing new and decrypts what was encrypted under it. The revocation is a register of its own, beside
-- derivation_label, which stays as the decision 0045 left it: a label is never changed nor dropped, and nothing here touches it. The register is read by everyone, as derivation_label is, and is
-- written only by the owner, who is the one that revokes; it takes only a label that is registered, and no role, the owner included, changes it, removes it or empties it.
CREATE TABLE public.derivation_label_revocation (
    label text PRIMARY KEY REFERENCES public.derivation_label (label),
    revoked_at timestamptz NOT NULL DEFAULT now(),
    revoked_by text NOT NULL DEFAULT session_user
);

GRANT SELECT ON public.derivation_label_revocation TO PUBLIC;

CREATE FUNCTION public.refuse_label_revocation_change() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    RAISE EXCEPTION 'a label revocation is never changed nor dropped (% on %)', TG_OP, TG_TABLE_NAME
        USING ERRCODE = 'restrict_violation';
END
$refuse$;

CREATE TRIGGER derivation_label_revocation_never_changed
    BEFORE UPDATE OR DELETE ON public.derivation_label_revocation
    FOR EACH ROW EXECUTE FUNCTION public.refuse_label_revocation_change();

CREATE TRIGGER derivation_label_revocation_never_emptied
    BEFORE TRUNCATE ON public.derivation_label_revocation
    FOR EACH STATEMENT EXECUTE FUNCTION public.refuse_label_revocation_change();

-- ALWAYS, so the refusal also holds in a session that set session_replication_role to replica, which skips an ordinary trigger.
ALTER TABLE public.derivation_label_revocation ENABLE ALWAYS TRIGGER derivation_label_revocation_never_changed;
ALTER TABLE public.derivation_label_revocation ENABLE ALWAYS TRIGGER derivation_label_revocation_never_emptied;

-- The event is about a domain, by its number, as the creation and the rotation are, and is the system's for an administrative act: the owner credential that revokes is no person of the registry.
-- Its reference is the label revoked, which names the purpose, the domain and the generation and nothing else. Only the checks change, from the last ones the earlier steps left (the step list and the
-- shape of a reference from the 0029, the shape of the domain from the 0021): every step they list and every clause they fix stays, and the new step comes in beside them. The shape of
-- the verification is the 0029's and stays: the new step is about a domain, not the chain.
ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_step_listed,
    DROP CONSTRAINT custody_event_domain_shape,
    DROP CONSTRAINT custody_event_emptying_shape,
    ADD CONSTRAINT custody_event_step_listed CHECK (step IN
        ('person_created', 'person_deactivated', 'person_function_changed', 'person_password_set', 'correction',
         'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'verify_chain', 'item_emptied', 'item_emptying_refused', 'epoch_rotated',
         'domain_created', 'seal_recorded', 'domain_label_rotated',
         'sign_in_accepted', 'sign_in_refused', 'credential_recomputed', 'key_loss_declared', 'recovery_tested', 'verify_guarantees',
         'owner_window_opened', 'owner_window_closed', 'restored', 'restore_rehearsed', 'domain_label_revoked')),
    ADD CONSTRAINT custody_event_domain_shape CHECK (
        (step IN ('domain_created', 'domain_label_rotated', 'domain_label_revoked')) = (item_kind = 'domain')
        AND (step NOT IN ('domain_created', 'domain_label_rotated', 'domain_label_revoked') OR (actor_system AND reason = 'administrative_act' AND corrects IS NULL))
        AND (step <> 'domain_created' OR reference IS NULL)
        AND (step <> 'domain_label_rotated' OR coalesce(reference ~ '^v[1-9][0-9]*$', false))
        AND (step <> 'domain_label_revoked' OR coalesce(reference ~ '^(texto-bruto|mapa-reversao)/[1-9][0-9]*/v[1-9][0-9]*$', false))
        AND (step <> 'domain_label_revoked' OR split_part(reference, '/', 2) = item_id::text)),
    ADD CONSTRAINT custody_event_emptying_shape CHECK (
        (reference IS NOT NULL)
            = (step IN ('item_emptied', 'item_emptying_refused', 'domain_label_rotated', 'key_loss_declared', 'owner_window_opened', 'owner_window_closed', 'restored', 'restore_rehearsed',
                        'domain_label_revoked'))
        AND (reference IS NULL OR step IN ('owner_window_opened', 'owner_window_closed', 'restored') OR reference ~ '^[A-Za-z0-9._/-]{1,64}$')
        AND (step NOT IN ('owner_window_opened', 'owner_window_closed', 'restored') OR reference ~ '^[a-z0-9=;]{1,160}$')
        AND (step <> 'restore_rehearsed' OR reference ~ '^[a-z]+(\.[a-z]+)*$')
        AND (step NOT IN ('item_emptied', 'item_emptying_refused') OR item_kind = 'item_text')
        AND (step = 'item_emptied') = (reason IN ('removal_requested_by_subject', 'contract_requirement'))
        AND (step = 'item_emptying_refused') = (reason IN ('author_without_function', 'item_already_emptied'))
        AND (step <> 'item_emptied' OR actor_person_id IS NOT NULL));

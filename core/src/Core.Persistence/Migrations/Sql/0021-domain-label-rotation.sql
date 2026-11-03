-- The rotation of the label of a domain (issue 56, part 1): the next generation of the two labels of a domain is written to derivation_label as the creation writes the first, and a generation
-- can be revoked. A revoked label encrypts nothing new and decrypts what was encrypted under it, so the revocation is a register of its own, append only, beside derivation_label, which stays as
-- the decision 0045 left it: a label is never changed nor dropped, and nothing here touches it.
CREATE TABLE public.derivation_label_revocation (
    label text PRIMARY KEY REFERENCES public.derivation_label (label),
    revoked_at timestamptz NOT NULL DEFAULT now(),
    revoked_by text NOT NULL DEFAULT session_user
);

-- Read by everyone, as derivation_label is: the code that encrypts asks it which labels it must not use. Written only by the owner, who is the one that rotates.
GRANT SELECT ON public.derivation_label_revocation TO PUBLIC;

-- Only an addition: a revocation is not undone, changed nor emptied, by any role, the owner included, and also in a session that set session_replication_role to replica.
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

ALTER TABLE public.derivation_label_revocation ENABLE ALWAYS TRIGGER derivation_label_revocation_never_changed;
ALTER TABLE public.derivation_label_revocation ENABLE ALWAYS TRIGGER derivation_label_revocation_never_emptied;

-- The two events are about a domain, by its number, as the creation is, and are the system's for an administrative act: the owner credential that rotates is no person of the registry.
-- The reference says what changed. The rotation carries the generation the domain passes to write under (v2), from which the two labels follow, since the purposes are closed and the domain is the
-- item; the revocation carries the label revoked. The reference keeps the character set of the protocol number, which both fit, and the name of the domain is in neither.
ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_step_listed,
    DROP CONSTRAINT custody_event_domain_shape,
    DROP CONSTRAINT custody_event_emptying_shape,
    ADD CONSTRAINT custody_event_step_listed CHECK (step IN
        ('person_created', 'person_deactivated', 'person_function_changed', 'person_password_set', 'correction',
         'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'verify_chain', 'item_emptied', 'item_emptying_refused', 'epoch_rotated',
         'domain_created', 'seal_recorded', 'domain_label_rotated', 'domain_label_revoked')),
    ADD CONSTRAINT custody_event_domain_shape CHECK (
        (step IN ('domain_created', 'domain_label_rotated', 'domain_label_revoked')) = (item_kind = 'domain')
        AND (step NOT IN ('domain_created', 'domain_label_rotated', 'domain_label_revoked') OR (actor_system AND reason = 'administrative_act' AND corrects IS NULL))
        AND (step <> 'domain_created' OR reference IS NULL)
        AND (step <> 'domain_label_rotated' OR coalesce(reference ~ '^v[1-9][0-9]*$', false))
        AND (step <> 'domain_label_revoked' OR coalesce(reference ~ '^(texto-bruto|mapa-reversao)/[1-9][0-9]*/v[1-9][0-9]*$', false))),
    ADD CONSTRAINT custody_event_emptying_shape CHECK (
        (reference IS NOT NULL) = (step IN ('item_emptied', 'item_emptying_refused', 'domain_label_rotated', 'domain_label_revoked'))
        AND (reference IS NULL OR reference ~ '^[A-Za-z0-9._/-]{1,64}$')
        AND (step NOT IN ('item_emptied', 'item_emptying_refused') OR item_kind = 'item_text')
        AND (step = 'item_emptied') = (reason IN ('removal_requested_by_subject', 'contract_requirement'))
        AND (step = 'item_emptying_refused') = (reason IN ('author_without_function', 'item_already_emptied'))
        AND (step <> 'item_emptied' OR actor_person_id IS NOT NULL));

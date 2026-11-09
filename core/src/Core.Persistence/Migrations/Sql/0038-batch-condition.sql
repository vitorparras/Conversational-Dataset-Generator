-- The condition under which the material came in (issue 85): the regime of use that its origin imposes, declared when the batch opens and valid for every item the batch brings. The column is the
-- batch's and the item takes it through the batch, so that no copy of it can diverge. The list of conditions the installation knows is configuration, and the database does not hold it: what it
-- guarantees is the shape of the name and that the condition of a batch never changes. That an opening with no condition takes the most restrictive one is the application's, with a test.

-- A batch that exists has no condition to give without inventing one, so the step refuses to apply while any batch exists, in any domain, as the 0035 does for the content.
DO $guard$
DECLARE
    declared integer;
BEGIN
    FOR declared IN SELECT id FROM public.domain LOOP
        PERFORM set_config('app.domain', declared::text, true);
        IF EXISTS (SELECT 1 FROM content.ingestion_batch) THEN
            RAISE EXCEPTION 'a batch exists that has no condition: the step 0038 does not apply over it';
        END IF;
    END LOOP;

    PERFORM set_config('app.domain', '', true);
END
$guard$;

ALTER TABLE content.ingestion_batch
    ADD COLUMN condition text NOT NULL,
    ADD CONSTRAINT ingestion_batch_condition CHECK (condition ~ '^[a-z][a-z0-9_]{0,62}$');

-- The application names the condition when it opens the batch, and no other column is added to what it may write.
GRANT INSERT (condition) ON content.ingestion_batch TO app;

-- The condition never changes after the opening, by any statement and any session, the owner and a replica one included, as the batch, the transaction and the domain of a row of content never do (0035).
CREATE FUNCTION public.refuse_batch_condition_change() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    IF NEW.condition IS DISTINCT FROM OLD.condition THEN
        RAISE EXCEPTION 'the condition of a batch never changes after its opening'
            USING ERRCODE = 'CE006', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    RETURN NEW;
END
$refuse$;

CREATE TRIGGER batch_condition_is_never_changed BEFORE UPDATE ON content.ingestion_batch FOR EACH ROW EXECUTE FUNCTION public.refuse_batch_condition_change();
ALTER TABLE content.ingestion_batch ENABLE ALWAYS TRIGGER batch_condition_is_never_changed;

-- The condition of an item, read beside the item: the view lives in the public schema, since nothing in the content one may be a view (a view has no row policy of its own), and it joins the item to its batch, by the key that carries the domain. It runs with the rights of who reads, so that the row policies of the two
-- tables apply as they do to a direct read: the item of a batch not accepted is not seen, and a domain sees only its own.
CREATE VIEW public.item_condition WITH (security_invoker = true) AS
    SELECT i.id AS item_id, i.domain_id, i.batch_id, b.condition
    FROM content.item_text i
    JOIN content.ingestion_batch b ON b.id = i.batch_id AND b.domain_id = i.domain_id;

GRANT SELECT ON public.item_condition TO PUBLIC;

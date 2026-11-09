-- The loss declared by an item whose material was accommodated into a form that is not exactly its own (issue 81): the form it received and what that form does not keep, beside the item and
-- not beside the batch, since two items of one batch are accommodated in different ways and a statement common to the batch would describe neither. Both are written in the very insert of the
-- item and never change, and neither holds a word of the material: the form is one of the four and each loss is a code of a closed set, whose text the application takes from the code; the losses are the codes joined by a comma, in the order of the set, and nothing when there is none.

-- An item with no form received is an item that fits the form of its material exactly, and holds no loss. Items written before the step are in that case, so it applies over any content.
ALTER TABLE content.item_text
    ADD COLUMN accommodated_into text,
    ADD COLUMN losses text NOT NULL DEFAULT '',
    ADD CONSTRAINT item_text_accommodated_into_listed
        CHECK (accommodated_into IS NULL OR accommodated_into IN ('conversation', 'comment', 'document', 'labeled_sequence')),
    ADD CONSTRAINT item_text_losses_are_codes
        CHECK (losses ~ '^([a-z][a-z0-9_]{0,62}(,[a-z][a-z0-9_]{0,62})*)?$'),
    ADD CONSTRAINT item_text_accommodation_declares_loss
        CHECK ((accommodated_into IS NULL) = (losses = ''));

-- What the item declares never changes after the insert, by any statement and any session, the owner and a replica one included, as the batch, the transaction and the domain of a row never do
-- (0035): the loss would otherwise be erased from an item that still carries what it lost.
CREATE FUNCTION public.refuse_item_accommodation_change() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    IF NEW.accommodated_into IS DISTINCT FROM OLD.accommodated_into OR NEW.losses IS DISTINCT FROM OLD.losses THEN
        RAISE EXCEPTION 'the form received and the loss of an item never change after the insert'
            USING ERRCODE = 'CE006', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    RETURN NEW;
END
$refuse$;

CREATE TRIGGER item_accommodation_is_never_changed BEFORE UPDATE ON content.item_text FOR EACH ROW EXECUTE FUNCTION public.refuse_item_accommodation_change();
ALTER TABLE content.item_text ENABLE ALWAYS TRIGGER item_accommodation_is_never_changed;

-- The refusal of the triggers of the step 0009 is also written in the log of the server, before the statement fails, so that an attempt to empty the table leaves a line there as an attempt
-- to change or remove an event does, and the line stays when the transaction is rolled back. It names the table, the operation and the role the session logged in as, and no value of the event.
-- Only the two functions are replaced; the triggers that call them, and the mode they are enabled in, are the ones of the step 0009 and are not touched here.
CREATE OR REPLACE FUNCTION public.refuse_custody_event_change() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    RAISE LOG 'public.custody_event: % refused for the role %', TG_OP, session_user;
    RAISE EXCEPTION 'public.custody_event: % refused for the event of sequence %', TG_OP, OLD.sequence
        USING ERRCODE = 'CE001', SCHEMA = 'public', TABLE = 'custody_event';
END
$refuse$;

CREATE OR REPLACE FUNCTION public.refuse_custody_event_emptying() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    RAISE LOG 'public.custody_event: % refused for the role %', TG_OP, session_user;
    RAISE EXCEPTION 'public.custody_event: % refused', TG_OP
        USING ERRCODE = 'CE001', SCHEMA = 'public', TABLE = 'custody_event';
END
$refuse$;

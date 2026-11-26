-- The refusal of an item stored before the coreference (issue 102, fase 5 fatia F, F3): when an occurrence it holds has no pair in the answer of the service, the rerun refuses it with
-- this reason, the item keeps no pseudonymized text, and the queue counts it; a new request queues it again.
ALTER TABLE content.sanitization_rerun_item DROP CONSTRAINT sanitization_rerun_item_refusal,
    ADD CONSTRAINT sanitization_rerun_item_refusal CHECK (refusal IN ('emptied', 'key_lost', 'failed', 'undecided', 'pseudonym_without_pair'));

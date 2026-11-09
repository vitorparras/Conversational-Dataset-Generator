-- The origin of a submission is stated with its receipt, never without (issue 75, part 2b'): the 0042 let the state of the origin be missing while the service that declares it was not yet in the stack,
-- and the part that brings the service tightens the check back, so that a batch a submission opens has the state of its origin and one that none opens has none.
ALTER TABLE content.ingestion_batch
    DROP CONSTRAINT ingestion_batch_origin_state,
    ADD CONSTRAINT ingestion_batch_origin_state CHECK ((submission IS NULL) = (origin_state IS NULL) AND (origin_state IS NULL OR origin_state IN ('declared', 'unknown')));

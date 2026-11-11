-- The repeat of a submission is recognized by the SHA-256 of what came, never by the name of the file (issue 80, CA-2 and CA-3): the question is whether a batch of the domain already accepted
-- that digest, and the index answers it without reading the batches of the domain one by one. It holds only the accepted ones, which are the only ones that can recognize a repeat.
-- It is unique, so that two identical files sent at the same time do not both get accepted: the batch that closes second is refused the acceptance by the database itself, and the application
-- turns that refusal into a repeat. A batch whose submission brought no digest (written before the receipt of the submission) is outside it, since a null equals nothing.
CREATE UNIQUE INDEX ingestion_batch_accepted_digest ON content.ingestion_batch (domain_id, received_digest) WHERE state = 'accepted';

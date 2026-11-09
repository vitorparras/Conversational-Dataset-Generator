-- The repeat of a submission is recognized by the SHA-256 of what came, never by the name of the file (issue 80, CA-2 and CA-3): the question is whether a batch of the domain already accepted
-- that digest, and the index answers it without reading the batches of the domain one by one. It holds only the accepted ones, which are the only ones that can recognize a repeat.
CREATE INDEX ingestion_batch_accepted_digest ON content.ingestion_batch (domain_id, received_digest) WHERE state = 'accepted';

-- The protected content: the text as it arrived, the sanitized text and the map that reverses a pseudonym are stored sealed by the application, as bytea the database cannot read;
-- the pseudonymized text is the one text left in clear. Each sealed column is marked with the purpose whose key seals it, and the startup check refuses a column that is neither marked nor listed in clear.
-- An item is emptied by setting emptied_at and clearing every content column, in clear and sealed alike; the marker is in clear, so that an emptied item is found without reading a sealed column.
SELECT public.create_content_table('item_text',
    'recorded_at timestamptz NOT NULL DEFAULT now(),
     emptied_at timestamptz,
     raw_text bytea,
     sanitized_text bytea,
     pseudonymized_text text,
     CONSTRAINT item_text_id_domain UNIQUE (id, domain_id),
     CONSTRAINT item_text_emptied CHECK ((emptied_at IS NULL AND raw_text IS NOT NULL)
         OR (emptied_at IS NOT NULL AND raw_text IS NULL AND sanitized_text IS NULL AND pseudonymized_text IS NULL))');

-- The map belongs to an item of the same domain: the key is composite, so that a map cannot point at an item of another domain, which a foreign key does not see through the row policy.
SELECT public.create_content_table('reversal_map',
    'item_id bigint NOT NULL,
     recorded_at timestamptz NOT NULL DEFAULT now(),
     emptied_at timestamptz,
     entries bytea,
     CONSTRAINT reversal_map_item_fkey FOREIGN KEY (item_id, domain_id) REFERENCES content.item_text (id, domain_id),
     CONSTRAINT reversal_map_emptied CHECK ((emptied_at IS NULL) = (entries IS NOT NULL))');

COMMENT ON COLUMN content.item_text.raw_text IS 'protected:texto-bruto';
COMMENT ON COLUMN content.item_text.sanitized_text IS 'protected:texto-bruto';
COMMENT ON COLUMN content.reversal_map.entries IS 'protected:mapa-reversao';

-- The application writes and reads content; changing it (emptying included) is granted by the step that empties.
GRANT SELECT, INSERT ON content.item_text, content.reversal_map TO PUBLIC;

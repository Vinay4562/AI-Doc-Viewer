-- Run once to create extension placeholders and simple tables
-- Note: pgvector extension setup must be done by DBA or cloud provider on managed PG.
CREATE TABLE IF NOT EXISTS documents(
  id bigserial PRIMARY KEY,
  title text,
  file_url text,
  status text,
  created_at timestamptz DEFAULT now()
);

CREATE TABLE IF NOT EXISTS chunks(
  id bigserial PRIMARY KEY,
  document_id bigint REFERENCES documents(id) ON DELETE CASCADE,
  page_no int,
  text text
);

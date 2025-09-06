CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE IF NOT EXISTS documents(
  id bigserial PRIMARY KEY,
  title text,
  file_url text,
  status text,
  created_at timestamptz DEFAULT now()
);

CREATE TABLE IF NOT EXISTS document_pages(
  id bigserial PRIMARY KEY,
  document_id bigint REFERENCES documents(id) ON DELETE CASCADE,
  page_no int,
  text text
);

-- chunks table with pgvector column (dimension 384)
CREATE TABLE IF NOT EXISTS chunks(
  id bigserial PRIMARY KEY,
  document_id bigint REFERENCES documents(id) ON DELETE CASCADE,
  page_no int,
  text text,
  embedding vector(384)
);

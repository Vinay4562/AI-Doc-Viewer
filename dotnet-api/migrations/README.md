EF Core migrations should be run from a dev machine with the dotnet SDK installed.
This folder contains a placeholder SQL file to create pgvector extension and initial tables if your Postgres allows it.

If using a managed Postgres (e.g., RDS), enable pgvector via your provider or run as a superuser.

Example (psql):
  CREATE EXTENSION IF NOT EXISTS vector;
  -- then run the SQL in init_pgvector.sql

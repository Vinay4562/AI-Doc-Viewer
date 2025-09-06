dotnet-api
This project is a minimal ASP.NET Core 8 gateway for the document assistant.
It exposes endpoints for upload and QA proxying to the python processor.

Setup:
- Ensure PostgreSQL is running and pgvector extension is available
- Update ConnectionStrings in appsettings.json
- Run EF Core migrations from your dev machine:
  dotnet tool install --global dotnet-ef
  dotnet ef migrations add InitialCreate
  dotnet ef database update

The repository includes migration SQL at ./migrations/init_pgvector.sql as a guide if you can't run migrations directly.

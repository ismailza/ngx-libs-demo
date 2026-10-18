---
name: ef-migration
description: Create, list or remove EF Core migrations for the server after changing entities or AppDbContext. Use when the data model changes or when asked to add/undo a migration.
---

# EF Core migrations

Migrations live in `server/src/Demo.Api/Data/Migrations/` and are generated code — never edit
them by hand. They run on startup (`Database:MigrateOnStartup`).

1. Make sure the tool is available: `dotnet ef --version`. If missing, ask before running
   `dotnet tool install --global dotnet-ef`.
2. Build first so errors surface clearly: `cd server && dotnet build NgxLibsDemo.slnx`.
3. Add (PascalCase name describing the change):

   ```bash
   cd server
   dotnet ef migrations add <Name> --project src/Demo.Api -o Data/Migrations
   ```

4. Read the generated `Up`/`Down` and check for unintended drops or renames-as-drop+add.
   Mention any data-loss risk to the user.
5. To undo the last migration that hasn't been shared yet:
   `dotnet ef migrations remove --project src/Demo.Api`.
6. Run `dotnet test NgxLibsDemo.slnx` — tests start a fresh Postgres and apply all migrations.

A local dev database that's out of sync can be reset with `docker compose down -v` — that
deletes data, so ask first.

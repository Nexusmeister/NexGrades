---
name: ef-migration
description: Adds and verifies an EF Core migration for NexGrades after an entity or configuration change, including a data-safety review of the generated code and a test run against a migrated SQLite database. Use whenever the data model in NexGrades.Data changes.
argument-hint: <MigrationName> (PascalCase, e.g. AddGradeWeightToBucket)
---

# Add an EF Core migration

All commands run from `src/NexGrades/`. `NexGrades.Data` contains both the model and the design-time
factory (`AppDbContextFactory`), so it is both the target and the startup project.

## 1. Make the model change

- Change the entity in `NexGrades.Data/Entities/` and update its XML doc comment if the meaning changed.
- Change its configuration in `NexGrades.Data/EntityMapConfigs/<Entity>Configuration.cs`. Don't use mapping
  attributes.
- Check the change against the invariants in `AGENTS.md`: no denormalized `SchoolYearId`, no cascade
  deletes that would remove history, `decimal` for grades and weights.

## 2. Generate the migration

```sh
dotnet ef --version   # if missing: dotnet tool install --global dotnet-ef
dotnet ef migrations add <MigrationName> --project NexGrades.Data --startup-project NexGrades.Data
```

Name it for what it does, in PascalCase: `AddNoteCategory`, not `Update3`.

## 3. Review the generated code before going on

Open `NexGrades.Data/Migrations/<timestamp>_<MigrationName>.cs` and check:

- **Data loss:** `DropColumn`, `DropTable` or a type change on a column holding real data. A teacher's
  past grades must survive. If the migration loses data, change the model approach instead.
- **New required columns:** existing rows need a sensible `defaultValue`. If no constant works, the
  migration needs a data step; describe it to the user rather than writing raw SQL unasked.
- **SQLite limits:** many `AlterColumn` changes make EF rebuild the table. That's fine, but check the
  generated rebuild keeps all data and indexes.
- **Closed years:** a migration that rewrites data in closed school years bypasses the audit interceptor.
  Flag it to the user.
- **`Down()`:** it must reverse `Up()` sensibly.

Never hand-edit the migration's `Designer.cs` or `AppDbContextModelSnapshot.cs`. To redo a migration
that hasn't been pushed, run
`dotnet ef migrations remove --project NexGrades.Data --startup-project NexGrades.Data`, fix the model and
generate it again. A migration that is already on `dev` is never removed; add a new one instead.

## 4. Verify

```sh
dotnet build NexGrades.App/NexGrades.App.csproj
dotnet test
```

`SqliteTestDatabase` migrates a fresh SQLite file for every test, so a passing test run proves that the
migration applies cleanly from an empty database. At app startup, `DatabaseMigrationService` applies it to
existing databases.

Report the migration name, what it does to existing data, and anything from step 3 the user needs to know.

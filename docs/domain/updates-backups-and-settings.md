# Updates, backups and settings

This page covers the preferences file, the database backup taken before a migration, the self-update cycle and
the Velopack pack id. The reasoning for updates is in
[0004: Application updates](../decisions/0004-application-updates.md).

## Settings file

`settings.json` in `%LOCALAPPDATA%\NexGrades\` (`IAppDataPaths.SettingsFilePath`) holds the preferences kept
outside the database: the update preferences, and the colour theme in `UserSettings.Theme`, which is stored by name.
`JsonUserSettingsStore` reads and writes it as `UserSettings`; a missing or unreadable file gives the defaults.
`LastRunVersion` is the version that last started against the default database, and it names the next backup.

The file stays in that folder when the database is overridden with `sqlite`. The `Settings:FilePath` configuration
key (environment variable `Settings__FilePath`) moves only `settings.json` to another file. A relative path is
resolved against the working directory. It is empty in shipped builds and is meant for screenshots and tests. The
database and the log files do not move with it; use `sqlite` for the database.

The Theme card on the Settings page applies a choice at once and saves it here. Three choices exist: **System**
(the default) follows the Windows light or dark setting while the app runs, and **Light** and **Dark** always use
that theme. `AppThemeService` applies the saved theme in `App.OnStartup`, before the main window is shown, so the
window does not open in the wrong theme. `WpfThemeApplier` wraps WPF-UI's `ApplicationThemeManager` and
`SystemThemeWatcher`; Light and Dark stop the watch. If the file cannot be read, the app starts in the System theme.
If a choice cannot be saved, it applies until the app closes.

## Backups

- Before a migration of an existing database (one with applied migrations), `DatabaseBackupService` copies the
  database with SQLite's online backup into `%LOCALAPPDATA%\NexGrades\backups`.
- The copy must pass `PRAGMA quick_check` before it gets its final name.
- `Backup:Directory` overrides the folder. With a `sqlite` override the folder is `backups` beside the overridden
  database.
- Files are named `app-yyyyMMdd-HHmmss-v<version>.db`, with `unknown` in place of the version when it is not
  known, and the five newest are kept.
- A failed backup stops startup before anything is migrated. When a migration fails after a backup, the startup
  dialog names the backup file.
- Nothing restores a backup automatically; `README.md` lists the manual steps.
- Backups hold pupil data, like the database (see
  [0001: Encryption at rest for the database](../decisions/0001-encryption-at-rest.md)).

## Updates

- `UpdateCoordinator` (`NexGrades.App/Updates/`) runs the update cycle. It stays inactive, and makes no network
  request, unless the app runs from a Velopack install, so development, the AppHost and the tests never check for
  updates.
- The feeds come from `Updates:Repository` in `NexGrades.App/appsettings.json`. The stable channel reads the
  release marked latest. The beta channel reads GitHub pre-releases and is checked only when `ReceiveBetaUpdates`
  is on.
- `Updates:SourceOverride`, a local folder or an http(s) URL, replaces the feed for every channel. It is empty in
  shipped builds and is meant for testing a locally packed version.
- With `AutoCheckForUpdates` on, a check runs about ten seconds after startup.
- A downloaded update is installed when the app exits, or at once when the user chooses Restart now in the main
  window's notice.
- The Settings page shows the version and both preferences and runs a manual check.
- `Program.Main` runs `VelopackApp.Build().Run()` before `App` is created; it handles the installer's hook
  arguments and returns on a normal start.

## Velopack pack id

`NexGradesPackId` in `NexGrades.App.csproj` (`NexGrades.App`) is the id the release packs with, and the app
carries it as the `VelopackPackId` assembly metadata. Velopack installs to `%LOCALAPPDATA%\<packId>` and deletes
that folder on uninstall, so the id differs from the data folder `%LOCALAPPDATA%\NexGrades`, and
`New-ReleaseAsset.ps1` refuses that name. Changing the id would leave installed apps unable to find their updates.

## Rules

See [Rules by area](../../AGENTS.md#rules-by-area) in `AGENTS.md`.

## Related

- [Startup, logging and data paths](startup-logging-and-data-paths.md)
- [0001: Encryption at rest for the database](../decisions/0001-encryption-at-rest.md)
- [0004: Application updates](../decisions/0004-application-updates.md)
- [Releasing](../releasing.md)

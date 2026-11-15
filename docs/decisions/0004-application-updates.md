# 0004: Application updates and database backups

- Status: Accepted
- Date: 2026-11-15
- Issue: #21

## Context

Teachers ran NexGrades from a plain zip and replaced it by hand for each release. Every release that changes the
schema runs migrations on the teacher's database at first start, so an update needs a copy of the data taken before
the migration, and a clear way back. The app is not code-signed, so Windows SmartScreen warns on first start (see
the release notes and `docs/releasing.md`).

## Options considered

1. **Keep the plain zip.** Nothing to build, but every update is a manual replacement. Rejected.
2. **Velopack (MIT) for install, update and packaging.** Chosen. It provides a per-user installer that needs no
   administrator rights, a portable bundle, a feed format the app reads, and a CLI (`vpk`) that packs the feed.
3. **GitHub's release API (`GithubSource`) for the stable channel.** Rejected. The API returns releases a page at a
   time, so the newest stable release has to be on the first page, and the check costs an API call each time. The
   fixed `releases/latest/download/` URL needs neither. Beta keeps `GithubSource`, because pre-releases are not
   reachable through that URL.
4. **Delta packages.** Not chosen for now. Each release stands alone, and the feed lists one package, which keeps the
   release check and the retention rules simple.
5. **A restore screen in the app.** Not built. The manual steps in `README.md` cover the rare case. A restore screen
   needs its own design, including how the teacher confirms that current data is replaced.
6. **Code signing now.** Deferred. There is no certificate yet. The release notes carry the SmartScreen note instead.

## Decision

### Install, packaging and channels

- Velopack 1.2.161 installs, updates and packs the app. The `vpk` tool in `.config/dotnet-tools.json` has the same
  version as the `Velopack` package in `Directory.Packages.props`. `New-ReleaseAsset.ps1` fails when they differ, so the
  packed packages and the in-app updater come from one release.
- Every release carries a per-user `Setup.exe`, a portable zip, the feed files (`releases.<channel>.json`,
  `RELEASES-<channel>`) and one full package. Packages are full only; no delta packages are built.
- The pack id is `NexGrades.App`. Velopack installs to `%LOCALAPPDATA%\<packId>` and deletes that folder on uninstall.
  The data folder `%LOCALAPPDATA%\NexGrades` holds the database, backups, logs and settings, so the pack id must not be
  `NexGrades`. Uninstalling keeps the data.
- Stable releases are published from `main`, beta pre-releases from `dev`. Installed apps read the channel they
  belong to.
- Stable checks read `releases/latest/download/` of the repository. Beta checks use Velopack's GitHub source for
  pre-releases; they are made only when the user turns on beta updates, and make one unauthenticated API call.
- Only versions newer than the installed one are offered (`UpdatePolicy.Choose`), and Velopack's `AllowVersionDowngrade`
  is off, so downgrades are not allowed. Turning beta updates off therefore never installs an older version.
- Releases are created as drafts with all assets and published afterwards. A failed run leaves a draft, which the next
  run for the same version replaces. Beta releases keep the ten newest; stable releases are never deleted.
- The app is unsigned. The release notes and `README.md` describe the SmartScreen prompt.

### Update behaviour in the app

- The updater is inactive unless the app runs from a Velopack install. Development, the AppHost and the tests make no
  update request.
- With automatic checks on, a check runs about ten seconds after startup. Each channel's check has a 30-second timeout.
- A found update downloads in the background. It is installed when the app exits. The main window's notice offers
  "Restart now", which first asks the current page whether it may be left, so unsaved input is not lost.
- An update downloaded earlier and not yet installed is taken up at the next start. Velopack also applies a leftover
  downloaded update at the next launch.
- The coordinator logs versions and channel names. Exception messages from Velopack, IO or HTTP can appear in the local
  file log only, as `docs/decisions/0003-application-logging.md` allows for framework and third-party exceptions; the
  OpenTelemetry export strips messages. Velopack's own logger is not bridged into the app log, because its messages
  contain local paths and URLs.

### Backups before a migration

- A backup is taken only before a migration of an existing database: one with applied migrations and pending ones.
  A new database, or one that is already up to date, is not backed up.
- The copy uses SQLite's online backup and must pass `PRAGMA quick_check`. It gets its final name only after that check,
  so a backup file that exists is complete.
- The folder is `%LOCALAPPDATA%\NexGrades\backups`. `Backup:Directory` overrides it. With a `sqlite` override the folder
  is `backups` beside the overridden database, so a development database never writes to the teacher's folder.
- Names are `app-yyyyMMdd-HHmmss-v<version>.db`, with `unknown` for the version when it is not known. The version is the
  one that last started against the default database (`LastRunVersion`). Five backups are kept: the newest ones, plus the oldest backup taken for the same previous app version, which is pinned so repeated failed starts cannot rotate it out (see `docs/domain/updates-backups-and-settings.md`).
- A backup that fails stops startup before the migration runs; the database is unchanged. A migration that fails after
  a backup ends startup, and the dialog names the backup file.
- Nothing restores a backup automatically. The manual steps are in `README.md`.
- A backup holds the same pupil data as the database. Its protection is the one described in
  `docs/decisions/0001-encryption-at-rest.md`.

### Settings and network

- `settings.json` sits in the data folder and is written through a temporary file, so an interrupted write leaves the
  old file. A file that cannot be read gives the defaults.
- An update check is an HTTPS request to GitHub. Besides what such a request carries, the client's address and
  Velopack's version and architecture query, it sends nothing about the teacher, grades or notes. The updater adds no
  telemetry; `docs/decisions/0002-telemetry-and-privacy.md` is unchanged.

## Consequences

- Each update downloads the full package. Old betas can be deleted, because no later update depends on them.
- The plain zip does not update itself. Its users install the Setup.exe or the portable zip once by hand.
- Backups use disk space: up to five copies of the database, next to the database itself.
- A backup that cannot be written blocks startup until the backup folder is writable and has space. For a database
  that is a file, the app does not migrate without a backup.
- A restore is a manual copy with the app closed. The teacher needs the steps in `README.md`.
- While the app is unsigned, SmartScreen warns on the installer and on the first start of the app.
- Velopack's own log lines are not in the app log. An update that fails inside Velopack shows up only as the
  coordinator's failure line.

## Revisit when

- A code-signing certificate is available. Then the SmartScreen notes in the release notes and `README.md` can go.
- Full downloads become a burden. Then consider delta packages; the feed and the release checks would need to handle
  a chain of packages.
- Teachers need to restore without help. Then a restore screen needs its own design and tests.
- The beta source needs more than the ten newest releases, or GitHub changes how its release list is served.
- Portable installs turn out not to update. Then fix that or document it.

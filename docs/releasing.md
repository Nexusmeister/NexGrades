# Releasing

Versions come from git tags, and the CI pipeline (`.github/workflows/ci.yml`) publishes releases to
GitHub Releases. Nothing is signed, and the pipeline needs no secrets beyond the built-in
`GITHUB_TOKEN`.

## How versions are computed

[MinVer](https://github.com/adamralph/minver) reads tags of the form `vX.Y.Z` (settings in
`NexGrades.App.csproj`).

| Commit | Version |
|---|---|
| tagged `v0.1.1` | `0.1.1` |
| 4 commits after `v0.1.1`, not tagged | `0.1.2-beta.4` |
| no stable tag reachable yet | `0.1.0-beta.<height>` (`MinVerMinimumMajorMinor` is 0.1) |

Beta release tags (`v0.1.2-beta.4`) are tags with the `v` prefix as well, so MinVer would build on the newest one
(`0.1.2-beta.4.7`). The pipeline therefore deletes them from its own clone (never from GitHub) before computing the
version, which keeps the table above true. A local clone that fetched the beta tags shows `X.Y.Z-beta.N.<height>`
versions; delete them locally if that bothers you (`git tag --list 'v*-beta.*'` piped to `git tag -d`).

## Betas (push to `dev`)

Only the tip of `dev` is published. Every push to `dev` that passes the test and build jobs publishes a GitHub
**pre-release** `vX.Y.Z-beta.<height>` with the assets attached, provided its commit is still the tip of `dev`. The
beta job checks this at the start and again right before it creates the release. If `dev` has moved on, it logs a
notice and publishes nothing, so an older commit is never published after a newer one; the newer push publishes its
own beta if it passes CI. The tip is read through the GitHub API with the workflow token
(`.github/scripts/Get-BranchTip.ps1`), so the job keeps working if the repository becomes private. Only the 10 newest
beta pre-releases are kept. Older ones are deleted together with their tags. The cleanup only considers pre-releases
whose tag matches `v*-beta.*`, sorts them by publish date and always keeps the one just published. Drafts are neither
counted nor deleted by the cleanup. If the dev tip carries a stable tag (for example after merging `main` back into
`dev`), the version has no pre-release part; the job then logs a notice and publishes nothing.

## Stable releases (push to `main`)

Every push (merge) to `main` that passes the test and build jobs publishes the next patch version as a stable
release (`v0.1.0` -> `v0.1.1`), marked as the latest release. The pipeline takes the highest stable tag (`vX.Y.Z`,
betas excluded) that is reachable from the merged commit (`git tag --merged`), adds one to the patch number, tags the
merged commit and builds the assets from that tag. **Stable releases and their tags are never deleted.**

Only the tip of `main` is released: the job reads the tip through the GitHub API with the workflow token
(`.github/scripts/Get-BranchTip.ps1`). If the pushed commit is no longer the current tip of `main` when the job runs (a
re-run, or a queued run that a newer push overtook), the job logs a notice and releases nothing, so older code never
receives a higher version than newer code. The newer push releases its own version.

**Skipped versions.** If the next patch tag already exists on a commit outside the history of the merged commit (for
example a tag pushed by hand on a commit that is only on `dev`), the job does not fail. It skips ahead to the next free
patch number, releases that, and logs a `::warning::` annotation naming the skipped tag(s). A stable tag on the merged
commit itself still releases exactly that tag. The choice is made by `.github/scripts/Get-StableReleaseTag.ps1`, which
reads the local tags, so the job fetches them first with the token passed for that one command; the tag fetch is
authenticated, so the job keeps working if the repository becomes private.

**First version.** With no stable tag at all, the first push to `main` becomes `v0.1.0`. To choose differently,
push the tag yourself before that merge (`git tag v0.1.0 <commit>` and `git push origin v0.1.0`); the next main
merge then releases `v0.1.1`.

**Minor or major bumps are tags you push by hand.** Tag the commit on `main` that should start the new line, with
patch 0:

```sh
git tag v0.2.0 <commit-on-main>
git push origin v0.2.0
```

- The next merge to `main` then releases `v0.2.1`, because the pipeline adds one to the highest stable tag reachable
  from the merged commit (or skips ahead, see *Skipped versions*).
- If `v0.2.0` itself should be a release, push the tag together with the commit it points to, so the tag is
  already on `HEAD` when the main workflow runs: `git push origin main v0.2.0`. The pipeline then releases exactly
  that tag instead of adding a patch. This needs a direct push to `main`. If `main` only accepts pull request
  merges, push the tag `v0.2.0` (on the commit the line should start from) before the next merge to `main`; that
  merge then releases `v0.2.1` and `v0.2.0` is not a release of its own. A tag pushed alone does not start a
  release.
- A manual tag on a commit that is only on `dev` does not set the base for the stable job until that commit is on
  `main`, but its version is taken: if it equals the next patch, the job skips it (see *Skipped versions*). Squash
  merges create a new commit, so tag the squashed commit on `main`.
- Betas follow the tags reachable from `dev`. After a release on `main`, merge `main` back into `dev` so the next
  betas continue from the new version.

## Assets

Each release, beta or stable, attaches the same set of files. They come from one self-contained Production publish
for win-x64 (no .NET install needed; `appsettings.Development.json` is left out), packed by the Velopack CLI (`vpk`):

| File | What it is |
|---|---|
| `NexGrades-<version>-win-x64-Setup.exe` | Per-user installer. It needs no administrator rights. |
| `NexGrades-<version>-win-x64-Portable.zip` | Portable bundle. Unzip it to a folder of your own and run `NexGrades.App.exe`. |
| `releases.<channel>.json`, `RELEASES-<channel>` | The update feed. Installed apps read these names, so they are not renamed. |
| `NexGrades.App-<version>-<channel>-full.nupkg` | The full package that the feed lists. |

Each release carries exactly one full package. No delta packages are built, so every update downloads the whole app,
and deleting an old beta removes nothing that a later update needs.

The release notes are generated from the merged pull requests and start with the install note, which is the
`SMARTSCREEN_NOTE` text in `ci.yml`.

The Velopack pack id is `NexGrades.App` (`NexGradesPackId` in `NexGrades.App.csproj`). Installs go to
`%LOCALAPPDATA%\NexGrades.App`, and uninstalling deletes that folder and keeps the data in `%LOCALAPPDATA%\NexGrades`.
Changing the pack id would leave installed apps unable to find their updates.

Releases from before the Velopack change attached one plain zip, `NexGrades-<version>-win-x64.zip`. That zip is no
longer published and has no update feed. Users of it install the Setup.exe or the portable zip once by hand.

## Update feeds

An installed app checks the feed of its channel and offers only versions newer than the installed one, so turning
beta updates off never installs an older version.

- The stable channel reads `releases/latest/download/` of the repository. GitHub serves the release marked as latest,
  so a check makes no GitHub API call and does not depend on the first page of the release list.
- The beta channel (only for users who turn on beta updates) uses Velopack's GitHub source for pre-releases. That makes
  one unauthenticated API request, which lists the ten newest releases.

Each release is created as a draft with all its assets and published after the upload. A run that fails before
publishing leaves a draft, and the next run for the same version replaces it. Installed apps therefore never see a
partly uploaded release.

## Packing locally

From the repository root, in PowerShell:

```powershell
./.github/scripts/New-ReleaseAsset.ps1 -OutputDirectory "$env:TEMP\nexgrades-release" -Channel beta
```

The script restores the packages and the tools in `.config/dotnet-tools.json`, publishes the App, packs it and checks
that the upload folder holds exactly the files listed above. It empties `-OutputDirectory` first and writes the upload
folder to `<OutputDirectory>\release`. The version is the one MinVer gives HEAD (see *How versions are computed*), so:

- `-Channel beta` needs a version `X.Y.Z-beta.N`. A clone that fetched the beta tags gets `X.Y.Z-beta.N.<height>`, and
  the script refuses it; delete those tags locally first.
- `-Channel stable` needs a plain `X.Y.Z`, so HEAD must carry the `vX.Y.Z` tag.

The `vpk` tool (Velopack 1.2.161) ships builds only for .NET 8, 9 and 10. Its entry in `.config/dotnet-tools.json`
sets `rollForward` to `true`, so it runs on its net10.0 build when a .NET 10 runtime is installed and rolls forward to
.NET 11 only where none is.

Pull request builds run the beta pack with the same script and upload nothing, so a packaging problem fails the pull
request.

## Testing an update locally

To test an update end to end, install an earlier release, pack a newer version locally (the rules above apply), and set
`Updates:SourceOverride` in the installed app's `appsettings.json` to the `release` folder. The installed app is in
`%LOCALAPPDATA%\NexGrades.App\current\`. The override replaces the feed for every channel, so pack the channel you test,
and turn beta updates on in Settings when testing a beta. The override is empty in shipped builds, so releases are not
affected.

## SmartScreen

The app is unsigned, so Windows SmartScreen may warn when the installer or the app starts for the first time. Choose
**More info**, then **Run anyway**.

## Dependency updates

Dependabot (`.github/dependabot.yml`) opens pull requests against `dev` every week for:

- the NuGet packages in `src/NexGrades/Directory.Packages.props`, including the transitive pins,
- the `vpk` dotnet tool in `.config/dotnet-tools.json`,
- the GitHub Actions in `.github/workflows/`,
- the .NET SDK pinned in `global.json`.

Dependabot writes the pull request titles itself (`Bump ...`), so no commit-message prefix is set. GitHub Actions and
the SDK each get one grouped pull request for their minor and patch updates. NuGet gets two: `nuget-minor-patch` holds
every other NuGet package, and `velopack` holds the Velopack packages. The `vpk` tool has its own ungrouped entry, so
each of its updates is one pull request. Each major update is its own pull request, so a breaking change is reviewed
alone. All groups set `applies-to: version-updates`, so a group never holds a security update back; security updates
open as soon as they are published.

Labels are set per entry, not per group. The Velopack package pull request therefore arrives labelled `dependencies` and
`nuget`; only the `vpk` pull request carries `velopack`.

Velopack (NuGet) and `vpk` must have the same version, because `New-ReleaseAsset.ps1` refuses to pack when they differ,
and the pack step of a pull request build fails with it. Dependabot opens them as separate pull requests. Add the
matching bump by hand to whichever of the two comes first: `.config/dotnet-tools.json` (`vpk` version) and
`src/NexGrades/Directory.Packages.props` (Velopack version). Dependabot stops rebasing a branch someone pushed to, so
close the other pull request by hand.

Dependabot ignores `Aspire.Hosting.AppHost`, `Aspire.Hosting.Dotnet` and `CommunityToolkit.Aspire.Hosting.Sqlite`. They
are raised together by hand in `src/NexGrades/Directory.Packages.props`. Major bumps would arrive as separate pull
requests, and the CommunityToolkit package ships on its own schedule, so the three could drift apart.

`src/NexGrades/Directory.Build.props` sets `EnableWindowsTargeting` to `true`. GitHub's dependency submission and
Dependabot run restore on Linux, and without the property the Windows-targeting projects fail to restore there. The
property has no effect on Windows builds.

**One-time setup:** the labels `dependencies`, `nuget`, `github-actions`, `dotnet-sdk` and `velopack` must exist in the
repository. Dependabot skips a label that does not exist.

**Merge gate:** a Dependabot pull request goes through the same gate as any other. All checks must be green, including
SonarCloud. A reviewer pass is required, with the `opus` model for major updates. If an App package changes, run a
smoke start (see the `smoke-start` skill). The lead squash-merges. A Dependabot pull request is never merged on green
alone.

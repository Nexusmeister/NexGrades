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

## Betas (push to `dev`)

Every push to `dev` that passes the test and build jobs publishes a GitHub **pre-release** `vX.Y.Z-beta.<height>`
with the asset attached. Only the 10 newest beta pre-releases are kept. Older ones are deleted together with their
tags. The cleanup only considers pre-releases whose tag matches `v*-beta.*`.

## Stable releases (push to `main`)

Every push (merge) to `main` that passes the test and build jobs publishes the next patch version as a stable
release (`v0.1.0` -> `v0.1.1`), marked as the latest release. The pipeline takes the highest existing stable tag
(`vX.Y.Z`, betas excluded), adds one to the patch number, tags the merged commit and builds the asset from that
tag. **Stable releases and their tags are never deleted.**

**First version.** With no stable tag at all, the first push to `main` becomes `v0.1.0`. To choose differently,
push the tag yourself before that merge (`git tag v0.1.0 <commit>` and `git push origin v0.1.0`); the next main
merge then releases `v0.1.1`.

**Minor or major bumps are tags you push by hand.** Tag the commit on `main` that should start the new line, with
patch 0:

```sh
git tag v0.2.0 <commit-on-main>
git push origin v0.2.0
```

- The next merge to `main` then releases `v0.2.1`, because the pipeline always adds one to the highest stable tag.
- If `v0.2.0` itself should be a release, push the tag together with the commit it points to, so the tag is
  already on `HEAD` when the main workflow runs: `git push origin main v0.2.0`. The pipeline then releases exactly
  that tag instead of adding a patch. A tag pushed alone does not start a release.
- A manual tag on a commit that is only on `dev` does not count for the stable job until that commit is on
  `main`. Squash merges create a new commit, so tag the squashed commit on `main`.
- Betas follow the tags reachable from `dev`. After a release on `main`, merge `main` back into `dev` so the next
  betas continue from the new version.

## Asset

`NexGrades-<version>-win-x64.zip`, a self-contained Production publish (no .NET install needed, no
`appsettings.Development.json`, no AppHost). Unzip it to a folder of your own and run `NexGrades.App.exe`. Release
notes are generated from the merged pull requests and carry the SmartScreen note.

Build the same asset locally from `src/NexGrades` with:

```sh
dotnet publish NexGrades.App -c Release -r win-x64 --self-contained -o <folder>
```

## SmartScreen

The app is unsigned, so Windows SmartScreen may warn on first start. Choose **More info**, then **Run anyway**.

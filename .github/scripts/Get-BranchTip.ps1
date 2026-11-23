<#
.SYNOPSIS
  Writes the commit at the tip of a branch of this repository on GitHub.

.DESCRIPTION
  The release jobs release only the tip of their branch, so an older run never publishes after a newer one. The tip
  comes from the GitHub API through gh, authenticated with GH_TOKEN, because the checkout does not persist its token
  and an unauthenticated git ls-remote would fail once the repository is private. Needs GH_TOKEN and GH_REPO
  (owner/name) in the environment; the contents: read permission is enough.

.PARAMETER Branch
  The branch name without refs/heads/, e.g. main or dev.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Branch
)

$ErrorActionPreference = 'Stop'

if (-not $env:GH_REPO) { throw 'GH_REPO is not set.' }
if (-not $env:GH_TOKEN) { throw 'GH_TOKEN is not set.' }

$sha = gh api "repos/$env:GH_REPO/git/ref/heads/$Branch" --jq '.object.sha'
if ($LASTEXITCODE -ne 0) { throw "Looking up the tip of ${Branch} failed with exit code $LASTEXITCODE." }
$sha = "$sha".Trim()
if ($sha -notmatch '^[0-9a-f]{40}$') { throw "Unexpected tip of ${Branch}: '$sha'." }
$sha

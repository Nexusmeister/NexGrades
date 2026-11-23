<#
.SYNOPSIS
  Decides which stable tag vX.Y.Z the stable release job publishes for a commit on main.

.DESCRIPTION
  Reads the local tags only, so fetch them first (git fetch --tags --force). Beta tags (vX.Y.Z-beta.N) and other
  tags that are not vX.Y.Z are ignored.

  - A stable tag on the commit itself (the owner pushed it with the commit) is released as it is; the highest wins.
  - Otherwise the next patch after the highest stable tag reachable from the commit is released, or v0.1.0 when no
    stable tag is reachable. A tag of that version can already exist on a commit outside the commit's history, such
    as a tag pushed by hand on another branch. The patch number is then raised until the version is free, so the job
    releases instead of failing, and the skipped tags are returned for the job to report. The free tag goes on the
    commit itself, so MinVer yields exactly its version there.

  Returns an object with:
    Tag          the tag to release
    CreateTag    $false when Tag is already on the commit, $true when the job must create it
    StartTag     the highest reachable stable tag other than Tag, where the release notes start; empty if none
    SkippedTags  the tags that were taken by commits outside the history, lowest first; empty if none

.PARAMETER Commit
  The commit to release (RELEASE_SHA in the workflow).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Commit
)

$ErrorActionPreference = 'Stop'

# Stable tags, lowest version first. $GitArgument narrows the list (--merged or --points-at a commit).
function Get-StableTagList
{
    param([string[]] $GitArgument = @())

    $tags = @(git tag --list 'v[0-9]*' @GitArgument)
    if ($LASTEXITCODE -ne 0) { throw "git tag --list failed with exit code $LASTEXITCODE." }
    @($tags | Where-Object { $_ -match '^v\d+\.\d+\.\d+$' } | Sort-Object { [version]$_.Substring(1) })
}

# Every stable tag in the clone, wherever it points: these versions are taken.
$taken = @(Get-StableTagList | ForEach-Object { [version]$_.Substring(1) })
# Only tags reachable from this commit count as released on its line; tags on other branches or later commits do not.
$reachable = @(Get-StableTagList -GitArgument '--merged', $Commit)
$onCommit = @(Get-StableTagList -GitArgument '--points-at', $Commit)

$skipped = [System.Collections.Generic.List[string]]::new()
if ($onCommit.Count -gt 0)
{
    $tag = $onCommit[-1]
    $createTag = $false
}
else
{
    if ($reachable.Count -eq 0)
    {
        $next = [version]::new(0, 1, 0)
    }
    else
    {
        $latest = [version]$reachable[-1].Substring(1)
        $next = [version]::new($latest.Major, $latest.Minor, $latest.Build + 1)
    }
    while ($taken -contains $next)
    {
        $skipped.Add("v$next")
        $next = [version]::new($next.Major, $next.Minor, $next.Build + 1)
    }
    $tag = "v$next"
    $createTag = $true
}
$earlier = @($reachable | Where-Object { $_ -ne $tag })
$startTag = if ($earlier.Count -gt 0) { $earlier[-1] } else { '' }

[pscustomobject]@{
    Tag = $tag
    CreateTag = $createTag
    StartTag = $startTag
    SkippedTags = $skipped.ToArray()
}

<#
.SYNOPSIS
  Publishes NexGrades.App self-contained for win-x64 and zips it as NexGrades-<version>-win-x64.zip.

.DESCRIPTION
  The version is whatever MinVer computes from the git tags at the checked-out commit (tag prefix v), so create
  any release tag locally BEFORE calling this script. The publish is a Production publish: the csproj leaves
  appsettings.Development.json out of it, and this script fails if that file still shows up.
  Writes `version` and `zip` to $env:GITHUB_OUTPUT when it runs on GitHub Actions.

.PARAMETER OutputDirectory
  Scratch directory for the publish output and the zip. Created, and emptied if it already exists.

.PARAMETER ExpectedVersion
  If given, the script fails unless MinVer yields exactly this version (guards the stable job against a missing tag).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $OutputDirectory,
    [string] $ExpectedVersion
)

$ErrorActionPreference = 'Stop'

function Assert-Success([string] $step)
{
    if ($LASTEXITCODE -ne 0) { throw "$step failed with exit code $LASTEXITCODE." }
}

$solutionDir = Join-Path $PSScriptRoot '..\..\src\NexGrades' | Resolve-Path
$project = Join-Path $solutionDir 'NexGrades.App\NexGrades.App.csproj'

# MinVer sets Version in its own target, so run that target and read the property back.
dotnet restore $project -r win-x64 | Out-Host
Assert-Success 'dotnet restore'
$version = (dotnet msbuild $project -nologo -t:MinVer -getProperty:MinVerVersion | Out-String).Trim()
Assert-Success 'MinVer'
if ($version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') { throw "MinVer returned an unexpected version: '$version'." }
if ($ExpectedVersion -and $version -ne $ExpectedVersion) { throw "MinVer returned '$version', expected '$ExpectedVersion'. Is the tag missing at this commit?" }
Write-Host "Version: $version"

if (Test-Path $OutputDirectory) { Remove-Item $OutputDirectory -Recurse -Force }
$publishDir = Join-Path $OutputDirectory 'NexGrades'
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

dotnet publish $project -c Release -r win-x64 --self-contained --no-restore -o $publishDir | Out-Host
Assert-Success 'dotnet publish'

if (Test-Path (Join-Path $publishDir 'appsettings.Development.json')) { throw 'appsettings.Development.json must not be part of a release.' }
if (-not (Test-Path (Join-Path $publishDir 'NexGrades.App.exe'))) { throw 'NexGrades.App.exe is missing from the publish output.' }

$zip = Join-Path $OutputDirectory "NexGrades-$version-win-x64.zip"
Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zip -CompressionLevel Optimal
Write-Host ("Created {0} ({1:N1} MB)" -f $zip, ((Get-Item $zip).Length / 1MB))

if ($env:GITHUB_OUTPUT)
{
    "version=$version" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
    "zip=$zip" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
}

<#
.SYNOPSIS
  Publishes NexGrades.App self-contained for win-x64 and packs it with Velopack into the release assets of one channel.

.DESCRIPTION
  The version is whatever MinVer computes from the git tags at the checked-out commit (tag prefix v), so create
  any release tag locally BEFORE calling this script. The beta channel requires a version X.Y.Z-beta.N and the stable
  channel a plain X.Y.Z. The publish is a Production publish: the csproj leaves appsettings.Development.json out of
  it, and this script fails if that file still shows up.

  vpk (the Velopack CLI, a local dotnet tool) packs the publish output into the upload folder:
    NexGrades-<version>-win-x64-Setup.exe     per-user installer (renamed from vpk's <packId>-<channel>-Setup.exe)
    NexGrades-<version>-win-x64-Portable.zip  portable bundle (renamed from vpk's <packId>-<channel>-Portable.zip)
    releases.<channel>.json, RELEASES-<channel>, <packId>-<version>-<channel>-full.nupkg
                                              the update feed; installed apps read these names, so they stay as vpk
                                              wrote them
  No delta packages are built: every release carries exactly one full package. vpk's assets.<channel>.json is moved
  out of the upload folder, because it lists the original Setup and Portable names and is not part of the feed.

  Writes `version` and `assets_dir` (the upload folder; upload every file in it) to $env:GITHUB_OUTPUT when it runs
  on GitHub Actions.

.PARAMETER OutputDirectory
  Scratch directory for the publish output and the upload folder. Created, and emptied if it already exists.

.PARAMETER Channel
  Velopack channel: beta or stable. Installed apps only update from releases of their own channel.

.PARAMETER ExpectedVersion
  If given, the script fails unless MinVer yields exactly this version (guards the stable job against a missing tag).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $OutputDirectory,
    [Parameter(Mandatory)] [ValidateSet('beta', 'stable', IgnoreCase = $false)] [string] $Channel,
    [string] $ExpectedVersion
)

$ErrorActionPreference = 'Stop'

function Assert-Success([string] $step)
{
    if ($LASTEXITCODE -ne 0) { throw "$step failed with exit code $LASTEXITCODE." }
}

$repoRoot = Join-Path $PSScriptRoot '..\..' | Resolve-Path
$solutionDir = Join-Path $repoRoot 'src\NexGrades' | Resolve-Path
$project = Join-Path $solutionDir 'NexGrades.App\NexGrades.App.csproj'

# MinVer sets Version in its own target, so run that target and read the property back.
dotnet restore $project -r win-x64 | Out-Host
Assert-Success 'dotnet restore'
$version = (dotnet msbuild $project -nologo -t:MinVer -getProperty:MinVerVersion | Out-String).Trim()
Assert-Success 'MinVer'
if ($version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') { throw "MinVer returned an unexpected version: '$version'." }
if ($ExpectedVersion -and $version -ne $ExpectedVersion) { throw "MinVer returned '$version', expected '$ExpectedVersion'. Is the tag missing at this commit?" }
if ($Channel -eq 'beta' -and $version -notmatch '^\d+\.\d+\.\d+-beta\.\d+$') { throw "The beta channel needs a version X.Y.Z-beta.N, but MinVer returned '$version'." }
if ($Channel -eq 'stable' -and $version -notmatch '^\d+\.\d+\.\d+$') { throw "The stable channel needs a version X.Y.Z, but MinVer returned '$version'." }
Write-Host "Version: $version (channel $Channel)"

# Velopack installs the app to %LOCALAPPDATA%\<packId> and removes that folder on uninstall. The pack id must
# therefore differ from NexGrades, whose %LOCALAPPDATA% folder holds the database and the logs. It must also never
# change, or installed apps would no longer find their updates.
$packId = (dotnet msbuild $project -nologo -getProperty:NexGradesPackId | Out-String).Trim()
Assert-Success 'Reading NexGradesPackId'
if (-not $packId) { throw 'NexGradesPackId is not set in NexGrades.App.csproj.' }
if ($packId -ieq 'NexGrades') { throw "NexGradesPackId must not be '$packId': uninstalling would delete the user's data folder %LOCALAPPDATA%\NexGrades." }
Write-Host "Pack id: $packId"

if (Test-Path $OutputDirectory) { Remove-Item $OutputDirectory -Recurse -Force }
$publishDir = Join-Path $OutputDirectory 'NexGrades'
$releaseDir = Join-Path $OutputDirectory 'release'
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

dotnet publish $project -c Release -r win-x64 --self-contained --no-restore -o $publishDir | Out-Host
Assert-Success 'dotnet publish'

if (Test-Path (Join-Path $publishDir 'appsettings.Development.json')) { throw 'appsettings.Development.json must not be part of a release.' }
if (-not (Test-Path (Join-Path $publishDir 'NexGrades.App.exe'))) { throw 'NexGrades.App.exe is missing from the publish output.' }

# The packages vpk builds must come from the same Velopack release as the updater inside the app.
$velopackVersion = @(Select-Xml -Path (Join-Path $solutionDir 'Directory.Packages.props') -XPath "//PackageVersion[@Include='Velopack']" |
    ForEach-Object { $_.Node.Version })
if ($velopackVersion.Count -ne 1 -or -not $velopackVersion[0]) { throw 'Directory.Packages.props must define exactly one Velopack PackageVersion.' }
$toolManifest = Join-Path $repoRoot '.config\dotnet-tools.json'
if (-not (Test-Path $toolManifest)) { throw "The dotnet tool manifest $toolManifest is missing." }
$vpkVersion = (Get-Content $toolManifest -Raw | ConvertFrom-Json).tools.vpk.version
if ($vpkVersion -ne $velopackVersion[0]) { throw "The vpk tool version '$vpkVersion' differs from the Velopack package version '$($velopackVersion[0])'." }

Push-Location $repoRoot
try
{
    # The tool manifest is at the repo root, so the package source mapping in src\NexGrades\nuget.config is not picked up by itself.
    dotnet tool restore --configfile (Join-Path $solutionDir 'nuget.config') | Out-Host
    Assert-Success 'dotnet tool restore'

    # A fresh output folder holds no earlier release, so vpk has nothing to build a delta from; --delta None makes
    # that explicit.
    dotnet vpk --skip-updates --legacyConsole pack --packId $packId --packVersion $version --packDir $publishDir `
        --mainExe NexGrades.App.exe --packTitle NexGrades --channel $Channel --runtime win-x64 --delta None `
        --outputDir $releaseDir | Out-Host
    Assert-Success 'vpk pack'
}
finally
{
    Pop-Location
}

# vpk names Setup and Portable after the pack id and channel only; give them the version, like the feed package.
$assetsJson = Join-Path $releaseDir "assets.$Channel.json"
if (-not (Test-Path $assetsJson)) { throw "vpk did not write $assetsJson." }
# Assigned before use: Windows PowerShell 5.1 emits a JSON array as one pipeline object, so @(...) would wrap it.
$packed = Get-Content $assetsJson -Raw | ConvertFrom-Json
$setupName = "NexGrades-$version-win-x64-Setup.exe"
$portableName = "NexGrades-$version-win-x64-Portable.zip"
foreach ($rename in @(@{ Type = 'Installer'; Name = $setupName }, @{ Type = 'Portable'; Name = $portableName }))
{
    $entry = @($packed | Where-Object { $_.Type -eq $rename.Type })
    if ($entry.Count -ne 1) { throw "vpk listed $($entry.Count) assets of type $($rename.Type), expected 1." }
    Rename-Item -LiteralPath (Join-Path $releaseDir $entry[0].RelativeFileName) -NewName $rename.Name
}
Get-ChildItem -LiteralPath $releaseDir -Filter 'assets.*.json' | Move-Item -Destination $OutputDirectory -Force

# The feed must describe exactly this one full package, or installed apps would be offered something else.
$feedName = "releases.$Channel.json"
$feedPath = Join-Path $releaseDir $feedName
if (-not (Test-Path $feedPath)) { throw "vpk did not write $feedName." }
$feedAssets = @((Get-Content $feedPath -Raw | ConvertFrom-Json).Assets)
if ($feedAssets.Count -ne 1 -or $feedAssets[0].Type -ne 'Full' -or $feedAssets[0].Version -ne $version)
{
    throw "$feedName must list exactly one full package of version $version, but lists: $(($feedAssets | ForEach-Object { "$($_.Type) $($_.Version)" }) -join ', ')."
}
$fullPackages = @(Get-ChildItem -LiteralPath $releaseDir -Filter '*-full.nupkg')
if ($fullPackages.Count -ne 1 -or $fullPackages[0].Name -ne $feedAssets[0].FileName) { throw "Expected exactly one full package, $($feedAssets[0].FileName); found $($fullPackages.Count)." }
if (@(Get-ChildItem -LiteralPath $releaseDir -Filter '*-delta.nupkg').Count -ne 0) { throw 'vpk built a delta package; releases ship full packages only.' }

# Everything in the upload folder is attached to the release, so it must hold exactly the expected files.
$expected = @($setupName, $portableName, $feedName, "RELEASES-$Channel", $fullPackages[0].Name) | Sort-Object
$actual = @(Get-ChildItem -LiteralPath $releaseDir -Force | ForEach-Object Name) | Sort-Object
if (Compare-Object $expected $actual) { throw "Unexpected upload folder content: $($actual -join ', '). Expected: $($expected -join ', ')." }
Get-ChildItem -LiteralPath $releaseDir | ForEach-Object { Write-Host ("Created {0} ({1:N1} MB)" -f $_.Name, ($_.Length / 1MB)) }

if ($env:GITHUB_OUTPUT)
{
    "version=$version" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
    "assets_dir=$releaseDir" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
}

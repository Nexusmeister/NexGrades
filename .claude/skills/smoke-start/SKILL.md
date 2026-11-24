---
name: smoke-start
description: Starts the built NexGrades.App against a throwaway database, checks the log to see that it starts cleanly, then stops it and removes the temp files. Use for every pull request that touches NexGrades.App, before reporting it merge-ready.
argument-hint: <worktree path>
---

# Smoke start

The App is a GUI, so a smoke start only shows that it starts. It does not replace the manual UI test plan.

The agent runs it, never the owner. It is required for every PR that touches `NexGrades.App`. It runs on the build
of the head SHA that will be reported: commit first, build (`dotnet build NexGrades.slnx` from `src/NexGrades/`),
then run it. Run it in PowerShell 5.1.

## The script

Set `$worktree` to the absolute worktree path and run the whole snippet. It replaces hand-written log filters; two
earlier hand-written filters had parsing bugs (one cut 29 instead of 30 characters). Log lines start with a
30-character timestamp, `yyyy-MM-dd HH:mm:ss.fff zzz`, so keep `Substring(0,30)` and the format string as they are.

```powershell
$worktree = '<absolute worktree path>'
$temp = Join-Path $env:TEMP ([guid]::NewGuid().ToString())
New-Item -ItemType Directory -Path $temp | Out-Null
$p = $null
try {
    $env:DOTNET_ENVIRONMENT = 'Development'
    $env:ConnectionStrings__sqlite = "Data Source=$temp\smoke.db"
    $start = [DateTimeOffset]::Now
    $p = Start-Process "$worktree\src\NexGrades\NexGrades.App\bin\Debug\net11.0-windows\NexGrades.App.exe" -WindowStyle Minimized -PassThru -WorkingDirectory $temp
    Start-Sleep 20

    $logDir = "$env:LOCALAPPDATA\NexGrades\logs"
    $day = $start.ToString('yyyyMMdd')
    $lines = @(foreach ($f in Get-ChildItem $logDir -Filter "nexgrades-$day*.log") { foreach ($l in Get-Content $f.FullName) { if ($l.Length -ge 30) { try { $ts = [DateTimeOffset]::ParseExact($l.Substring(0,30), 'yyyy-MM-dd HH:mm:ss.fff zzz', [Globalization.CultureInfo]::InvariantCulture); if ($ts -ge $start) { $l } } catch { } } } })

    $starting = @($lines | Where-Object { $_ -match 'starting on' })
    $errors = @($lines | Where-Object { $_ -match '\[(ERR|FTL)\]' })
    $warnings = @($lines | Where-Object { $_ -match '\[WRN\]' })
    "lines since start: $($lines.Count)"
    "starting-on lines: $($starting.Count)"
    "ERR/FTL lines: $($errors.Count)"
    "WRN lines: $($warnings.Count)"
    "process exited: $($p.HasExited)"
    $errors
    $warnings
}
finally {
    if ($p -and -not $p.HasExited) { Stop-Process -Id $p.Id -Force }
    Remove-Item Env:\DOTNET_ENVIRONMENT, Env:\ConnectionStrings__sqlite -ErrorAction SilentlyContinue
    Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
}
```

The ERR, FTL and WRN lines are printed on purpose: a fresh database holds no pupil data.

## Steps

1. Run the script above. It creates a fresh temp folder, sets the environment so the run stays off the real
   `%LOCALAPPDATA%` database (which must never be used), starts the app, waits 20 s, reads only this run's log
   lines, and stops the app and deletes the temp files in `finally`.
2. Judge the result from the printed counts.
   - Pass: the "starting on" count is at least 1, the ERR/FTL count is 0 and the process has not exited.
   - A failed start shows a modal message box and exits with code 1 only after the box is closed, so the process
     can look alive. The log decides.
   - On a fresh database an EF `[WRN]` about `PRAGMA foreign_keys` is expected and is not a failure.
   - If `[ERR]` or `[FTL]` appears, check that it comes after this run's "starting on" line before calling it a
     failure.
3. Never stop the app by image name, because the owner may have the app open. The script uses
   `Stop-Process -Id $p.Id -Force`.

## After the run

- A failed smoke start is a blocker. Send it to the owning agent like a failed build.
- If later commits change App code (CI or review fixes), rebuild, then repeat the smoke start before reporting.
- Record the result (pass, fail or not applicable) for the PR body and the final report.

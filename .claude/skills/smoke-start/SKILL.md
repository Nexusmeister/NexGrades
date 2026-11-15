---
name: smoke-start
description: Starts the built NexGrades.App against a throwaway database, checks the log to see that it starts cleanly, then stops it and removes the temp files. Use for every pull request that touches NexGrades.App, before reporting it merge-ready.
argument-hint: <worktree path>
---

# Smoke start

The App is a GUI, so a smoke start only shows that it starts. It does not replace the manual UI test plan.

The agent runs it, never the owner. It is required for every PR that touches `NexGrades.App`. It tests the
working tree that will be committed, after a build (`dotnet build NexGrades.slnx` from `src/NexGrades/`). Run it
in PowerShell 5.1.

## Steps

1. Create a fresh, empty temp folder for this run under the session scratchpad or `$env:TEMP`.
2. Set `$env:DOTNET_ENVIRONMENT='Development'` and
   `$env:ConnectionStrings__sqlite="Data Source=<temp folder>\smoke.db"`. The connection string keeps the run off
   the real `%LOCALAPPDATA%` database, which must never be used.
3. `$start = [DateTimeOffset]::Now`.
4. Start the app and wait about 20 s. The working directory keeps the run out of the build folder.

   ```powershell
   $p = Start-Process "<worktree>\src\NexGrades\NexGrades.App\bin\Debug\net10.0-windows\NexGrades.App.exe" -PassThru -WorkingDirectory "<temp folder>"
   ```

5. Read all of today's `%LOCALAPPDATA%\NexGrades\logs\nexgrades-<yyyyMMdd>*.log` files. The folder is shared with
   the owner's session, and files roll over to `_001`. Parse each line's timestamp as a `DateTimeOffset` (the
   format is `yyyy-MM-dd HH:mm:ss.fff zzz`, with a UTC offset). Judge only lines at or after `$start`, and look
   for this run's `NexGrades <version> starting on` line.
6. Judge the result.
   - Pass: the process is still alive, and this run's log lines contain the "starting on" line and no `[ERR]` or
     `[FTL]`.
   - A failed start shows a modal message box and exits with code 1 only after the box is closed, so the process
     can look alive. The log decides.
   - If `[ERR]` or `[FTL]` appears in the window, check that it comes after this run's "starting on" line before
     calling it a failure.
7. Stop the app with `Stop-Process -Id $p.Id -Force`. Never kill by image name, because the owner may have the
   app open.
8. Delete the temp folder, including `smoke.db` and any `-journal`, `-wal` or `-shm` files.

## After the run

- A failed smoke start is a blocker. Send it to the owning agent like a failed build.
- If later commits change App code (CI or review fixes), rebuild, then repeat the smoke start before reporting.
- Record the result (pass, fail or not applicable) for the PR body and the final report.

# 0001: Encryption at rest for the database

- Status: Accepted
- Date: 2026-10-27
- Issue: #50 (follow-up from #42)

## Context

NexGrades keeps grades and notes about minors in a local SQLite file on the teacher's own device. In
Germany, state rules for processing student data on private devices usually require the principal's
approval and protection of the data, typically encryption. Details vary by state. The file is currently
unencrypted.

## Options considered

1. **OS-level device encryption** (BitLocker / Windows device encryption). Nothing to build; protects
   the file while the device is off or locked, and covers every other file on the drive. Does not protect
   copies or backups made to unencrypted media, and the user must have it enabled.
2. **App-level encryption** (SQLCipher, e.g. `SQLitePCLRaw.bundle_e_sqlcipher`). The database file is
   encrypted on its own, including in copies and backups. Costs: key handling (startup password or
   Windows DPAPI, with lost-password risk), a migration path for existing unencrypted databases, tests,
   and replacing the `SQLitePCLRaw.bundle_e_sqlite3` package, which is pinned in
   `Directory.Packages.props` for a security advisory.

## Decision

Rely on OS-level device encryption for now (option 1). The README tells the user to enable it. The app
does not encrypt the database and does not check the encryption state.

## Consequences

- No code, schema or package changes.
- Data protection depends on the user's device setup. Copies of the `.db` file on unencrypted media are
  unprotected, and the README says so.
- Whether this satisfies a given state's or school's rules is the teacher's responsibility to confirm
  with the principal.

## Revisit when

- A school or state authority requires encryption that travels with the file (backups, exports, USB).
- The app gains backup/export or sync features that copy the database off the device.
- The app is used on shared or unmanaged devices where device encryption cannot be assumed.
- A startup warning for a non-encrypted system drive is wanted (smaller step than SQLCipher).

If revisited and SQLCipher is chosen, it needs an in-place migration of existing unencrypted databases,
a key-handling design, and tests, as listed in #50.

# NexGrades
Grade Maker

## Features

- School years (Planned, Active, Closed). Closing a year and activating the next one is a single step, and a closed
  year is read-only until you unlock it.
- Classes, students and subjects per school year, with weighted grade buckets and averages.
- A grade sheet with assessments. Final grades are proposed from the subject average and can be overridden.
- Notes on students, archived instead of deleted.
- Excel exports: the overview of one subject on the class page ("Export to Excel"), and a student's notes for a
  date range on the student page ("Export notes…", Notes tab). The notes export has its own dialog for the range
  and for including archived notes.

## Releases

Each push to `dev` publishes a beta pre-release and each merge to `main` a stable release, as a self-contained
Windows zip on GitHub Releases. See [docs/releasing.md](docs/releasing.md).

## Data protection

NexGrades stores grades and notes about students in a local SQLite database. The app does not encrypt
this file itself. Before you use it with real student data:

- Turn on device encryption for the drive that holds the database (BitLocker or Windows device
  encryption: Settings > Privacy & security > Device encryption, or BitLocker in Control Panel).
- Do not copy the database file or its backups to unencrypted media such as plain USB sticks or cloud folders.
- Check your state's and school's rules for student data on private devices. Many require the
  principal's approval.

Background and when this will be revisited: [docs/decisions/0001-encryption-at-rest.md](docs/decisions/0001-encryption-at-rest.md).

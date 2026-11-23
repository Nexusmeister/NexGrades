# German glossary

Status: **draft for owner review.** No German UI string has been translated yet. The terms below are proposals.
Items marked **(choice n)** are decisions the owner must confirm or change; they are listed in
[Choices for the owner](#choices-for-the-owner). Once confirmed, this page is the source for the German
resource strings of issue #26.

Each row gives the English UI text as it is today (or as planned, where the screen does not exist yet), the German
proposal, and notes. The English column is quoted from the code where it exists.

## Style rules

- Address the teacher with the formal **Sie** form in full sentences ("Möchten Sie die Klasse wirklich löschen?"
  is wrong, because nothing is deleted; use "Möchten Sie diese Klasse archivieren?").
- Buttons and menu items use the **infinitive**: "Speichern", "Klasse hinzufügen", "Notiz archivieren".
- Questions are phrased as "Möchten Sie ...?" (for example "Möchten Sie die Eingabe verwerfen?").
- Use German quotation marks „…“ in running text, never "…" or ‘…’.
- Put a non-breaking space before `%` (for example `10 %`, with U+00A0 between the number and the sign).
- Use a trailing "..." only for actions that open a dialog or a page that asks something first (for example
  "Schuljahr abschließen ..."). Actions that run at once get no dots.
- Grade tokens ("1+", "2-") are culture-neutral and are never translated.
- Format numbers, averages and dates with the current UI culture. German uses the decimal comma (`2,3`) and dates
  such as `01.12.2025`. Persisted and audit values stay invariant.
- Never build a sentence from fragments. Use one format string with placeholders such as `{0}` and `{1:d}`, because
  word order differs between languages.
- **False friend:** the English **Note** (a written remark about a pupil) is **Notiz**. In German, **Note** means a
  grade only. The app's `Note` entity is "Notiz" in the UI.
- Use "Schülerin / Schüler" only where the gender form matters (see choice 1). Elsewhere use the plural
  "Schülerinnen und Schüler".

## Terms

### Navigation

| English UI term | German term | Notes and alternatives |
|---|---|---|
| Home | Startseite | |
| Students | Schülerinnen und Schüler | Sidebar label. Singular "Schüler/in" in forms (choice 1). |
| Classes | Klassen | |
| Subjects | Fächer | |
| School year | Schuljahr | |
| Settings | Einstellungen | |
| Search | Suchen | Placeholder text, no dots. |

### School year

| English UI term | German term | Notes and alternatives |
|---|---|---|
| school year | Schuljahr | Labels such as "2026/27" stay as they are. |
| active school year | aktives Schuljahr | State: Active. Only one year is active at a time. |
| planned school year | geplantes Schuljahr | State: Planned. |
| closed school year | abgeschlossenes Schuljahr | State: Closed. Writes are locked (see Lock). |
| next school year | nächstes Schuljahr | |
| label | Bezeichnung | For example "2026/27". |
| first day | erster Schultag | |
| last day | letzter Schultag | |
| end school year | Schuljahr beenden | Used for the step before closing. |
| close school year and activate next | Schuljahr abschließen und nächstes aktivieren ... | Opens the close flow, so it ends in "...". Alternative: "Schuljahr abschließen und nächstes Schuljahr aktivieren". |
| Step "{0} of 6" (format) | Schritt {0} von 6 | Use one format string with `{0}`. |
| Step title: Overview | Übersicht | Step 1 of 6. |
| Step title: Next school year | Nächstes Schuljahr | Step 2 of 6. |
| Step title: Classes | Klassen | Step 3 of 6. |
| Step title: Readiness check | Abschlusskontrolle | Step 4 of 6. Not "Abschlussprüfung": that means a final exam. "Prüfung" also reads as exam, so avoid it (choice 8). |
| Step title: Confirm | Bestätigen | Step 5 of 6. |
| Step title: Done | Fertig | Step 6 of 6. |
| This cannot be undone | Dies lässt sich nicht rückgängig machen. | Full sentence, so it ends with a full stop. |
| Warnings | Warnungen | Items that do not block the step. |
| Blocking items | Hindernisse | Items that stop the close. Alternative: "Blockierende Punkte". |
| I have reviewed these warnings | Ich habe diese Warnungen geprüft. | Checkbox label. |
| Readiness | Abschlusskontrolle | Same term as the step title. |

### Lock (closed-year lock)

| English UI term | German term | Notes and alternatives |
|---|---|---|
| locked | gesperrt | |
| unlock year | Schuljahr entsperren | Button, so no dots if it runs at once. The unlock dialog itself ends in "...". |
| lock again | wieder sperren | Button. |
| read-only | nur lesbar | Banner text: "Dieses Schuljahr ist nur lesbar." |
| unlock for | entsperren für | Label before the duration options. |
| 1 hour | 1 Stunde | |
| until the end of today | bis zum Ende des heutigen Tages | |
| 1 week | 1 Woche | |
| custom date | bestimmtes Datum | Alternative: "bis zu einem Datum". |
| reason | Grund | Stored with the unlock and shown in the audit. |
| end date | Enddatum | Date until which the year stays unlocked. |
| closed on | abgeschlossen am | Format: "abgeschlossen am 30.07.2026". |

### Classes

| English UI term | German term | Notes and alternatives |
|---|---|---|
| class | Klasse | |
| class name | Klassenname | Maximum 20 characters. |
| cohort | Klassenverband | The persistent group across years, for example "Jahrgang 5" (choice 7). |
| students in this class | Schülerinnen und Schüler dieser Klasse | Section heading. |
| joined | eingetreten am | Enrollment start date, for example "eingetreten am 01.08.2026". |
| enrolled | zugeordnet | Current membership. Alternative: "eingeschrieben" (sounds administrative). |
| left the class | hat die Klasse verlassen | Enrollment end. Used with a date: "(hat die Klasse verlassen am 01.12.2025)". See also Excel exports. |
| not enrolled | nicht zugeordnet | Student has no class in this year. |
| roll forward | in das nächste Schuljahr übernehmen | Not "versetzen": that means promoting a pupil to the next grade (choice 6). |
| rolled forward as | übernommen als | For example "übernommen als 6A". |
| archive | archivieren | Class or note; never delete. |
| archived | archiviert | |
| end this class | diese Klasse beenden | Only for a class in the active year. |
| carries over | wird übernommen | Used for students or classes that continue in the next year. |

### Students

| English UI term | German term | Notes and alternatives |
|---|---|---|
| student | Schülerin / Schüler | Gender form, see choice 1. Alternatives: "Schüler", gender star, "Lernende". |
| first name | Vorname | |
| last name | Nachname | |
| exempt | befreit | Exempt from grading in a subject. Use "von der Benotung befreit" in full sentences. |
| not enrolled in any school year | keinem Schuljahr zugeordnet | Shown when a student has no enrollment at all. |

### Subjects

| English UI term | German term | Notes and alternatives |
|---|---|---|
| subject | Fach | |
| subject name | Fachname | For example "Mathematik". |
| short code | Kürzel | For example "M" or "Ma". |

### Grades

| English UI term | German term | Notes and alternatives |
|---|---|---|
| grade | Note | Culture-neutral tokens "1+", "2-" stay as they are. |
| grade sheet | Notenübersicht | The screen with one row per student and one column per assessment. |
| grade bucket, grade category | Bewertungsbereich | Alternative: "Leistungsbereich" (choice 4). The English UI will use "grade category" (choice 4). |
| weight | Gewichtung | For example "Gewichtung 40 %". |
| average | Durchschnitt | Shown with a decimal comma: "2,3". An empty bucket shows no average. |
| proposal | Vorschlag | The proposed final grade, derived from the average. Column header today: "Proposal". |
| final grade | Zeugnisnote | Alternative: "Endnote" (choice 2). Column header today: "Final grade". |
| override | überschrieben, manuell festgelegt | Noun: "Überschreibung". Use "manuell festgelegt" in the grade cell tooltip. |
| set by teacher | von der Lehrkraft festgelegt | Marker for a final grade that overrides the proposal. Column header today: "Final grade set by teacher". |
| void | streichen | Alternative: "ungültig setzen" (choice 3). The item is kept, not deleted. |
| voided | gestrichen | Shown in a grade cell; a voided grade is not counted. |
| restore | wiederherstellen | Undoes void and archive. |
| show voided grades | gestrichene Noten anzeigen | Toggle on the student page. |
| assessment | Leistungsnachweis | One graded occasion, for example "Klassenarbeit 1". See choice 5. |
| add assessment | Leistungsnachweis hinzufügen | Button. |
| edit assessment | Leistungsnachweis bearbeiten | Button. |
| void assessment | Leistungsnachweis streichen | Button. |
| title | Titel | Assessment title, for example "Bruchrechnung". |
| label | Bezeichnung | Short name shown in the column header. |
| comment | Kommentar | |
| date | Datum | Shown as "01.12.2025". |

### Notes

| English UI term | German term | Notes and alternatives |
|---|---|---|
| note | Notiz | Free-text remark about a student. Never "Note". |
| new note | neue Notiz | Button: "Neue Notiz". |
| edit note | Notiz bearbeiten | |
| archive note | Notiz archivieren | Removed from the normal list, can be restored. |
| restore note | Notiz wiederherstellen | |
| show archived notes | archivierte Notizen anzeigen | Toggle. |
| export notes | Notizen exportieren ... | Opens a dialog, so the dots stay. |
| from | von | Start of the date range. |
| to | bis | End of the date range. |
| include archived notes | archivierte Notizen einbeziehen | Checkbox in the export dialog. |

### Excel exports

| English UI term | German term | Notes and alternatives |
|---|---|---|
| student | Schülerin / Schüler | Column header. Use the same form as in the UI (choice 1). |
| subject average | Fachdurchschnitt | Alternative: "Durchschnitt {0}" with the subject name, as in the "{0} average" pattern. |
| "{0} average" (format) | Durchschnitt {0} | One format string with the subject name. |
| final grade | Zeugnisnote | Column header. Same term as the UI (choice 2). |
| final grade set by teacher | Zeugnisnote von der Lehrkraft festgelegt | Column header. |
| Yes | Ja | Cell value for the "set by teacher" column. |
| "(left on {date})" (format) | (hat die Klasse verlassen am {0:d}) | Appended to a student name. One format string. |
| exported on | exportiert am | Label in the notes export. |
| category | Bewertungsbereich | Column header. Same term as the UI (choice 4). |
| last edited | zuletzt bearbeitet | Column header in the notes export. |
| archived on | archiviert am | Column header. |
| Excel workbook | Excel-Arbeitsmappe | Used in messages such as "Exportiert als Excel-Arbeitsmappe". |

### Dialogs and snackbars

| English UI term | German term | Notes and alternatives |
|---|---|---|
| Save | Speichern | Button. |
| Cancel | Abbrechen | Button. |
| Back | Zurück | Button or navigation. |
| Continue | Weiter | Button. |
| Apply | Übernehmen | Button. |
| Open | Öffnen | Button. |
| Discard | Verwerfen | Button in a dialog about unsaved input. |
| Discard changes | Änderungen verwerfen | Button text. |
| Keep editing | Weiter bearbeiten | Button text. |
| Success | Erfolg | Snackbar title. |
| Error | Fehler | Snackbar title. |
| unsaved changes | nicht gespeicherte Änderungen | For example "Sie haben nicht gespeicherte Änderungen." |
| could not be saved | konnte nicht gespeichert werden | Used as "Die Notiz konnte nicht gespeichert werden." |
| could not be loaded | konnte nicht geladen werden | Used as "Die Notenübersicht konnte nicht geladen werden." |
| the school year is closed | Das Schuljahr ist abgeschlossen. | Full sentence, Sie form when addressing the user. |

### Settings

| English UI term | German term | Notes and alternatives |
|---|---|---|
| theme | Design | Alternative: "Farbschema". |
| System (theme) | System | Follows Windows. |
| Light | Hell | |
| Dark | Dunkel | |
| language | Sprache | |
| System (language) | System | Follows Windows. |
| English | English | Language names are always shown in their own language. |
| Deutsch | Deutsch | Language names are always shown in their own language. |
| save location | Speicherort | Folder where the database is kept. |
| log files | Protokolldateien | Alternative: "Logdateien". |
| open log folder | Protokollordner öffnen | Button. |
| updates | Updates | Section heading. Alternative: "Aktualisierungen". |
| automatically check for updates | Automatisch nach Updates suchen | Checkbox. |
| receive beta updates | Beta-Versionen erhalten | Checkbox. |
| check for updates now | Jetzt nach Updates suchen | Button. |
| restart | Neu starten | Button. |
| Restart NexGrades to use the new language | Starten Sie NexGrades neu, um die neue Sprache zu verwenden. | Full sentence, Sie form. |
| Version {0} will be installed when you close NexGrades | Version {0} wird installiert, wenn Sie NexGrades schließen. | One format string with the version. |
| up to date | auf dem neuesten Stand | Status text. |
| offline | offline | Status text when no connection exists. |
| only available in the installed app | Nur in der installierten App verfügbar. | Shown when the update check is not possible from a development build. |

### Startup error box

| English UI term | German term | Notes and alternatives |
|---|---|---|
| could not start | NexGrades konnte nicht gestartet werden. | Title of the message box. |
| data folder writable | Der Datenordner ist nicht beschreibbar. | Cause text. |
| backed up | gesichert | As in "Die Datenbank wurde gesichert." |
| backup folder | Sicherungsordner | |
| log folder | Protokollordner | Same term as "open log folder". |

## Choices for the owner

These are proposals made by the orchestrator. Confirm each one or name the alternative. Each choice is
referenced by number in the tables above.

1. **Gender form for students.** Proposal: "Schülerin / Schüler" for the singular label, and
   "Schülerinnen und Schüler" for the plural. Alternatives: the generic "Schüler", a gender star ("Schüler*innen"),
   or "Lernende".
2. **Final grade.** Proposal: "Zeugnisnote". Alternative: "Endnote".
3. **Void.** Proposal: "streichen" and "gestrichen". Alternative: "ungültig setzen".
4. **Bucket.** Proposal: "Bewertungsbereich". Alternative: "Leistungsbereich". The English UI will be unified to
   "grade category" in the same change, so the German term matches it.
5. **Assessment.** Proposal: "Leistungsnachweis".
6. **Roll forward.** Proposal: "in das nächste Schuljahr übernehmen". Not "versetzen", which means promoting a pupil.
7. **Cohort.** Proposal: "Klassenverband".
8. **Readiness check.** Proposal: "Abschlusskontrolle". "Abschlussprüfung" is wrong because it means a final exam,
   and "Prüfung" in general also reads as exam.

Terms not listed as choices but still open for review: "theme" (Design), "log files" (Protokolldateien), and the
Settings and startup-box wording.

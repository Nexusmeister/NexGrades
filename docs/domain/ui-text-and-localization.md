# UI text and localization

The App shows English and German. All user-facing text lives in resource files and is looked up by key, in XAML
and in C#. This page covers the file layout, the key scheme, how to add a string or an area, the culture rules and
the startup order, the language setting, the Excel exports, the tests and how to translate. This page holds the
rules for user-facing text; [AGENTS.md](../../AGENTS.md#ui-text-and-localization) points here.

## Languages

- English is the neutral language. German is the satellite (`.de.resx`) of every resource file.
- The language setting has three values: **System** (the default), **English** and **Deutsch**. **System** uses
  German when the Windows display language is German, and English otherwise. The setting is described under
  [Language setting](#language-setting).
- A language change applies after a restart. The running app never switches language.

## Files

Each area has its own resource pair, next to the code that uses it:

| Area | Resource files | Holds |
|---|---|---|
| `Common` | `Localization/CommonStrings.resx` (and `.de.resx`) | Buttons and titles shared by many views, such as `Common_Save` |
| `Core` | `Core/CoreStrings.resx` | Shared dialogs and the year lock, such as the unlock dialog |
| `Infrastructure` | `Infrastructure/InfrastructureStrings.resx` | The startup error box and the Excel save dialog filter |
| `Shell` | `Shell/ShellStrings.resx` | The main window, the navigation and the update notice |
| `Home` | `Features/Home/HomeStrings.resx` | The Home page |
| `Classes` | `Features/Classes/ClassesStrings.resx` | The class pages |
| `Subjects` | `Features/Subjects/SubjectsStrings.resx` | The subject pages |
| `Students` | `Features/Students/StudentsStrings.resx` | The student pages, including the Grades and Notes tabs |
| `Grades` | `Features/Grades/GradesStrings.resx` | The grade sheet and the grade cells |
| `SchoolYears` | `Features/SchoolYears/SchoolYearsStrings.resx` | The school year pages and the close flow |
| `Settings` | `Features/Settings/SettingsStrings.resx` | The Settings page |
| `Export` | `Features/Export/ExportStrings.resx` | The export dialogs and the text of the export files |

The code that reads the text is in `NexGrades.App/Localization/`:

- `TextResources.cs`: the registry of areas and the lookup (`Get`, `Text`, `Format`).
- `TextExtension.cs`: the `{loc:Text}` markup extension for XAML.
- `UiCultureResolver.cs`: chooses the two cultures from the saved setting and the Windows cultures.
- `UiCultureApplier.cs`: applies the cultures to the process at startup.
- `AppLanguageService.cs`: loads and saves the setting, and reports whether a restart is pending.
- `UiCultures.cs` and `LanguagePreference.cs`: the value types of the two cultures and the setting.

Strings are looked up by key, not through generated typed classes. WPF compiles XAML before MSBuild generates those
classes, so they are not visible to the markup. The same lookup serves C# so that both work the same way.

## Key scheme

A key is `<Area>_<Context>_<Name>[_Role]`, for example `Settings_Language_Header` or
`Settings_Updates_RestartNow_Tooltip`.

- **Area**: the registered area name. The part before the first underscore selects the resource file, so an area
  name never contains an underscore.
- **Context**: the page, dialog or part the text belongs to, for example `Overview`, `StudentPage` or `Grid`.
- **Name**: what the text says, for example `Empty` or `FirstName`.
- **Role** (optional): the kind of text, for example `Header`, `Tooltip` or `Title`.
- **`_Format` suffix**: used exactly when the text has `{n}` placeholders, and not otherwise. A `_Format` key needs a
  `<comment>` in the English resource that names each placeholder, for example `{0} is the maximum number of
  characters.`
- **Plurals**: a count that needs two forms uses two keys, `_One_Format` (exactly one) and `_Many_Format` (none or
  more than one), for example `SchoolYears_Class_CarryOver_One_Format` and `SchoolYears_Class_CarryOver_Many_Format`.
  The code picks the key from the count.
- **No sentence fragments**: a sentence is one key with placeholders. Code never joins two keys or two strings to
  make a sentence, because word order differs between languages.

The key must start with its area's name followed by an underscore. `ResourceFileTests` checks this.

## Adding a string

Never write literal UI text in XAML or C#. Add the English and German keys together, in the same change.

1. Add the key to the English file of its area (`<Area>Strings.resx`). Add a `<comment>` if the key is a `_Format`
   key.
2. Add the same key to the German file (`<Area>Strings.de.resx`), translated as described under
   [Translating](#translating).
3. Use the key:
   - In XAML, with the `loc` namespace declared as `xmlns:loc="clr-namespace:NexGrades.App.Localization"`:
     `Text="{loc:Text Classes_Overview_Empty}"`. The text is read once, when the XAML loads.
   - In C#, with `TextResources.Text("<key>")`, or `TextResources.Format("<key>", args)` for a `_Format` key.
     `TextResources.Get("<key>", culture)` reads a key in a given culture; the exports use it.
4. Run `dotnet test` from `src/NexGrades/`. A key that is typed wrongly, or that no source uses, fails the tests.

A key that is missing from the English file throws `InvalidOperationException` when it is read. The message names
only the key. A typo in a key fails `ResourceKeyUsageTests` at test time, so it does not wait for the view to load.
A German file must have every English key, and `ResourceFileTests` fails when one is missing. The lookup falls back
to English for a key that is missing from German, but that fallback is a safety net, not a translation strategy.

## Adding an area

1. Create `<Area>Strings.resx` and `<Area>Strings.de.resx` in the folder of the feature, with the file name ending in
   `Strings`.
2. Register the area with one line in the `_baseNames` table in `TextResources.cs`. The value is the manifest name of
   the English file: the root namespace, the folder with dots, and the file name without `.resx`, for example
   `NexGrades.App.Features.Students.StudentsStrings`.
3. Give every key the area's prefix.

`ResourceFileTests.EveryResxArea_IsRegistered` fails when a file and the registry disagree.

## Culture rules

The App uses two cultures:

- The **UI culture** chooses the text (the satellite assembly). It is the `Ui` value of `UiCultures`.
- The **format culture** formats numbers, averages and dates. It is the `Format` value of `UiCultures`.

`UiCultureResolver.Resolve` chooses them:

| Setting | UI culture | Format culture |
|---|---|---|
| System | The Windows display language | The Windows regional format |
| English | `en` | The Windows regional format when it is English, otherwise `en-GB` |
| Deutsch | `de` | The Windows regional format when it is German, otherwise `de-DE` |

So a German-speaking user with a Swiss or Austrian region keeps that region's formats, and a German-language user
with an English region still gets German text with German formats. `UiCultureResolverTests` pins these cases.

Further rules:

- Averages use the decimal comma in German (`2,3`). Dates follow the format culture.
- `TextResources.Format` reads the text in the UI culture and formats the arguments in the format culture.
- Persisted and audit values use `CultureInfo.InvariantCulture`, as before. They are never shown as text.
- Grade tokens such as `1+` and `2-` are culture-neutral and are never translated.
- Log messages, exception messages and telemetry stay in English and never go through resources. Exception messages
  are diagnostic text and are not translated, so don't design UI that shows them verbatim. See
  [Startup, logging and data paths](startup-logging-and-data-paths.md#privacy).
- Close readiness is reported as structured `CloseReadinessIssue` values. `ReadinessItemText.Format(issue, textCulture,
  formatCulture)` words them in the text culture (the UI culture on the page) and formats their numbers, such as a
  weight total, in the format culture.

## Startup order

`App.xaml.cs` (`OnStartup`) sets the language before anything shows text or formats a date:

1. The host is created (logging, crash handlers, telemetry services, the services and the database registration).
2. The startup lines are logged and the telemetry listeners are created.
3. `AppLanguageService.InitializeAsync` reads the saved setting and resolves the cultures. It never throws: an
   unreadable file means the System setting.
4. `UiCultureApplier.Apply` applies the cultures.
5. The legacy database is relocated, the database is backed up and migrated, the Active year is bootstrapped, the
   theme is applied and the host starts.

The language is set after telemetry and before the relocation, the backup and the migration. A startup failure before
step 4 shows the error box in the Windows language. See
[Startup, logging and data paths](startup-logging-and-data-paths.md#startup-sequence).

`UiCultureApplier.Apply` sets the `CultureInfo.DefaultThreadCurrent*` values as well as the current cultures. The
default values give every new thread and the thread pool the same cultures, and they survive the `await` in
`OnStartup`. It also replaces the WPF default of `FrameworkElement.LanguageProperty` once per process, so XAML
bindings format in the format culture. That replacement can happen only once, so a later call leaves it unchanged.

## Language setting

- The Settings page has a **Language** card with a combo box: **System**, **English** and **Deutsch**.
- A choice is saved at once in `settings.json`, as `UserSettings.Language`, which is stored by name (`System`,
  `English` or `German`). See [Updates, backups and settings](updates-backups-and-settings.md#settings-file).
- The running app keeps the language it started with. The card then shows the restart notice
  (`Settings_Language_RestartNotice`). There is no restart button for the language yet. It is a follow-up.
- A choice that cannot be saved shows a caution snackbar. The saved value does not change.

## Excel exports

The exports take their cultures when they are written. Text comes from the UI culture. Numbers and dates come from the
format culture, with the exceptions noted below.

- The class overview. `SubjectOverviewExporter.ExportAsync` passes `CultureInfo.CurrentUICulture` and
  `CultureInfo.CurrentCulture` to `SubjectOverviewExcelWriter.Write(table, output, uiCulture, formatCulture = null)`.
  The writer takes from the UI culture the column headers and the `Yes` marker in the "Final grade set by teacher"
  column. It formats the date in the "left on" marker, which is appended to a student's name, in the format culture;
  `null` means `CultureInfo.CurrentCulture`. The sheet name is built from the subject, class and school year and is
  not translated.
- The notes export. `NoteExporter.WriteAsync` passes `CultureInfo.CurrentUICulture` to
  `NoteExcelWriter.Write(table, output, uiCulture)`. The writer takes the sheet name, the labels above the table and
  the headers from the UI culture. It has no format culture: its dates are Excel dates, see below.
- The suggested file names. `NoteExporter.SuggestFileName(table, uiCulture)` localizes only its leading word; its dates
  are always `yyyy-MM-dd`. `SubjectOverviewExporter.SuggestFileName` is not localized.

The dates of the notes export and the date range are written as Excel dates with Excel's built-in short date formats.
Excel shows them in the locale of the Excel installation, not in the app's format culture. Grades and averages are
written as numbers with a fixed format (`0` for the final grade, `0.0` for the averages), and Excel shows their decimal
separator in its own locale too. See [Exports](exports.md).

## Tests

`NexGrades.App.Tests` covers the localization code:

- `ResourceFileTests` reads the real `*Strings*.resx` files and checks that:
  - every neutral file has a German file, and every German file has exactly the neutral keys;
  - the German values are not empty, and differ from the English values unless the key is on the allow-list
    `_sameInGerman` (the language names, `System` and a few loan words, each with a reason in that list);
  - the placeholders of a key are the same in both languages, and every value parses as a composite format string;
  - a key ends in `_Format` exactly when its text has placeholders, and every `_Format` key has a comment;
  - every area is registered in `TextResources`, and every key starts with its area's prefix;
  - the German satellite assembly contains the neutral keys for every registered area.
- `TextResourcesTests`, `UiCultureResolverTests`, `AppLanguageServiceTests` and `LanguageSettingsStoreTests` cover the
  lookup, the culture rules, the saving and loading of the setting.
- The `*GermanTextTests` files (for example `StudentsGermanTextTests`) check the German text of a feature. Each test
  sets `CurrentUICulture` and `CurrentCulture` itself and restores them afterwards.
- `TestCultureInitializer` pins the test process to en-US with a module initializer, so the tests give the same
  formats and text on a German machine.
- `ResourceKeyUsageTests` scans every `.xaml` and `.cs` file of `NexGrades.App` (not `obj` or `bin`) and checks that:
  - every key a source refers to exists in the neutral resx of its area. A reference is a `{loc:Text Key}` in XAML,
    the literal first argument of `TextResources.Text`, `Format` or `Get` in C#, and any C# string literal that is a
    whole key of a registered area, so both branches of `cond ? "A_B" : "A_C"` count. Keys named in comments do not.
    A key passed in a variable is not seen, so write every key out in full;
  - every key of a neutral resx is referred to by some source, so a key nobody uses fails the test. Delete it, with
    its German text. The allow-list `_dynamicKeys` for keys built at run time is empty;
  - no C# code sets `Title`, `Content`, `PrimaryButtonText`, `SecondaryButtonText` or `CloseButtonText` to a string
    literal with a letter, and no such literal is the first or second argument of a `Show` or `ShowAsync` call. The
    brand name `NexGrades` is the one allowed literal. Other properties, such as `Text` or `Header`, are not checked.
- `XamlSourceRulesTests` checks the XAML of the App:
  - no text attribute (`Content`, `Header`, `Text`, `ToolTip`, `PlaceholderText`, `Title`, `Message`, the three
    button texts, `AutomationProperties.Name` and `AutomationProperties.HelpText`) holds a literal with a letter, and
    no `StringFormat` holds literal text. Only attributes are read: text inside an element, such as `<Run>Save</Run>`,
    is not checked. The test fails if no `{loc:Text}` is found, so it cannot pass without checking anything;
  - the `Title` of a page's root element (a root with `x:Class`) does not equal the class name and does not look like
    an identifier, such as `ClassPage` or `settings_header`. A `{loc:Text}` title is read in English first.
- `XamlLoadTests` creates every page (every type that implements `INavigableView<>`) and the other views it lists
  (`HomePage`, `MainWindow`, `YearLockBar` and the two dialogs), on an STA thread, once under en-US and once under
  de-DE. A missing `{loc:Text}` key, or a view that does not load, fails the test. A key used only in a data template
  is not created by this test, but `ResourceKeyUsageTests` still checks that it exists.

The tests check that keys exist and are used, and that the places listed above hold no literal text. They do not check
the wording of the German text or its layout. Those are reviewed by hand, as described under
[Translating](#translating) and [Design system](design-system.md#text-and-localization).

## Translating

Translate with [the German glossary](german-glossary.md). It fixes the terms, the form of address (**Sie**), the
infinitive for buttons, the quotation marks and the spacing before `%`, and it lists the choices the owner confirmed.

- Keep the same placeholders in the German text. They must have the same indices, and they may appear in another
  order.
- Keep a `_Format` key's comment in the English file. It tells the translator what each placeholder holds.
- Do not translate a grade token (`1+`, `2-`).
- German text is about 30% longer than English. Check the layout of the page with the German text, as described in
  [Design system](design-system.md#text-and-localization).
- If a German text must equal the English one on purpose, add the key to `_sameInGerman` in `ResourceFileTests`,
  with a comment that says why.
- The German strings are reviewed by the owner before a merge.

## Related

- [German glossary](german-glossary.md)
- [Design system](design-system.md)
- [Exports](exports.md)
- [Startup, logging and data paths](startup-logging-and-data-paths.md)
- [Updates, backups and settings](updates-backups-and-settings.md)
- [0002: Telemetry and privacy](../decisions/0002-telemetry-and-privacy.md)
- [0003: Application logging](../decisions/0003-application-logging.md)

## Code

- `NexGrades.App/Localization/`
- `NexGrades.App/Features/Settings/LanguageSettingsViewModel.cs`
- `NexGrades.App/Features/Settings/LanguageOption.cs`
- `NexGrades.App/Features/SchoolYears/ReadinessItemText.cs`
- `NexGrades.App/Infrastructure/UserSettings.cs`
- `NexGrades.App/Infrastructure/StartupFailureHandler.cs`
- `NexGrades.App/App.xaml.cs`
- `NexGrades.App/Features/Export/SubjectOverviewExcelWriter.cs`
- `NexGrades.App/Features/Export/NoteExcelWriter.cs`
- `NexGrades.App.Tests/Localization/`
- `NexGrades.App.Tests/Composition/XamlLoadTests.cs` and `XamlSourceRulesTests.cs`

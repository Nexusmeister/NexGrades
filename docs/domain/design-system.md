# Design system

The design system is the set of shared WPF resources that every view uses for spacing, type, colour, icons and the
empty and status states. It lives in `src/NexGrades/NexGrades.App/DesignSystem/`. It holds no business rules. For
those, see [where the rules live](README.md#where-the-rules-live).

## Purpose and scope

- One spacing scale, one type ramp and one colour rule, so every page looks like one app in the Light and Dark
  themes.
- It covers the App's XAML resources. The theme itself is not part of it: `App.xaml` starts the WPF-UI Dark
  theme, and `WpfThemeApplier` applies the saved choice (System, Light or Dark).
- Fourteen views merge the design system: the feature pages, `MainWindow`, `YearLockBar` and the two dialogs
  `UnlockYearDialog` and `ExportNotesDialog`. Some literal values remain in views. See [Known gaps](#known-gaps).

## Files

| File | Contents |
|---|---|
| `Tokens.xaml` | Values only: spacing, margins, paddings, sizes, the radius and the font face. Keys start with `Ng.`. |
| `Styles.xaml` | Merges `Tokens.xaml` and holds the shared styles. |
| `DesignSystem.xaml` | Merges `Styles.xaml` and declares the shared converters. This is the one file a view merges. |

The converters are `BooleanToVisibilityConverter` (WPF's own class), `InverseBooleanToVisibilityConverter` and
`NonEmptyStringToVisibilityConverter` (in `NexGrades.App.Core`). Only `DesignSystem.xaml` declares them. A view
must not declare its own copy.

## Wiring

### Each view merges the file itself

Every view merges `DesignSystem.xaml` into its own resources. A page does this in `Page.Resources`:

```xml
<Page.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="/NexGrades.App;component/DesignSystem/DesignSystem.xaml" />
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Page.Resources>
```

`App.xaml` merges only the WPF-UI theme and `Shell/ShellControlStyles.xaml`. It does not merge the design system, so
a view gets these keys only from its own merge. The reasons:

- The tests share one `Application`. `XamlLoadTests` creates each view under a test application that holds the
  WPF-UI theme, the WPF-UI control styles and `ShellControlStyles.xaml`, but never `DesignSystem.xaml`. The
  application is created once and shared by the tests of the run. `StudentPage` notes that the page must build
  without the `App.xaml` resources.
- Views stay self-contained. A page that merges its own dictionary builds the same way wherever it is hosted and
  does not depend on what `App.xaml` happens to hold.

A missing `StaticResource` throws while the view loads. `XamlLoadTests` therefore fails for a view that uses a key
it does not merge.

### StaticResource or DynamicResource

- Use `StaticResource` for `Ng.*` keys (tokens, styles and converters). They do not change at run time.
- Use `DynamicResource` for every theme brush. A brush must update when the user switches theme.

### Pitfalls

- **A style cannot target `ui:TextBlock`.** WPF-UI replaces the `Style` of a `ui:TextBlock` at run time, so a style
  that sets `Visibility` on one never applies. No style in this design system targets `ui:TextBlock`. Set the type
  with `FontTypography` and `Appearance` on the element, and bind `Visibility` directly.
  `XamlSourceRulesTests` fails for an inline or keyed style that sets `Visibility` on a `ui:TextBlock`.
- **No `BasedOn` with `DynamicResource`.** `BasedOn` takes a `StaticResource` or a type. A `DynamicResource` there
  fails when the view loads. The shared styles that extend a WPF-UI style use
  `BasedOn="{StaticResource {x:Type ui:Card}}"` and the like.

## Tokens

| Key | Value | Used for |
|---|---|---|
| `Ng.Space.XS`, `S`, `M`, `L`, `XL` | 4, 8, 16, 24, 32 | The spacing scale. The keys are defined, but no view references them yet. |
| `Ng.Margin.Page` | 24 on all sides | The page margin, set by `Ng.PageRoot` |
| `Ng.Margin.ActionBar` | 0,16,0,8 | Around an action bar (`Ng.ActionBar`) |
| `Ng.Margin.ActionItem` | 0,0,8,8 | Each button in an action bar |
| `Ng.Margin.Block` | 0,0,0,16 | Between blocks: cards, form fields and the page header |
| `Ng.Margin.Section` | 0,0,0,24 | Between sections (`Ng.Section`) |
| `Ng.Margin.Related` | 0,0,0,8 | Between related controls, and below a heading |
| `Ng.Margin.Tight` | 0,0,0,4 | Between a label and its field, and below a page title |
| `Ng.Margin.InlineGap` | 0,0,8,0 | Between controls side by side |
| `Ng.Margin.Tile` | 0,0,16,16 | The gap after a tile card (`Ng.Card.Tile`) |
| `Ng.Margin.ColumnStart` | 16,0,0,0 | The left edge of a second column |
| `Ng.Padding.Card` | 16 | Inside a card or an item surface |
| `Ng.Padding.Cell` | 8,4 | Inside a grade sheet cell |
| `Ng.Margin.CellText` | 12,0 | The text inset of a data grid cell (`Ng.DataGridText`); 12 lines it up with the WPF-UI column header. It is on the 4 grid (`DesignSystemRulesTests` enforces multiples of 4) but off the 8 grid |
| `Ng.Radius.Control` | 4 | The corner radius of an item surface |
| `Ng.Size.RowHeight` | 32 | The minimum height of a data grid row and a grade cell |
| `Ng.Size.FormMaxWidth` | 480 | The widest form (`Ng.FormPanel`) |
| `Ng.Size.TileWidth` | 320 | The width of a tile card (`Ng.Card.Tile`) |
| `Ng.Size.MultilineMinHeight`, `MultilineMaxHeight` | 72, 120 | The height range of a multi-line text box |
| `Ng.FontFamily` | Segoe UI Variable Text, Segoe UI | The font face, used by `Ng.PageRoot`. The file's comment says it matches the WPF-UI content font. |

The `Ng.Space.*` scale has no step of 12 (it runs 4, 8, 16, 24, 32), although 12 is a valid multiple of 4. Use the keys above, not a literal number, and add a token when you need a value that
is not here (see [When to add a token or a style](#when-to-add-a-token-or-a-style)).

## Type ramp

Set the type with `FontTypography` and never with `FontSize`. The ramp has five steps. Each element uses one of
them:

| Step | Used for | Example |
|---|---|---|
| `Title` | The page title, the header text of `Ng.PageHeader` | Every page |
| `Subtitle` | Section headings (`Ng.Section`), an editor title inside a card, a subject name in the student's grades | `ClassPage`, `StudentPage` |
| `BodyStrong` | Group headings in a form, setting names, a final grade or average, a column title in the grade sheet, a bucket name | `SettingsPage`, `StudentPage`, `GradeSheetPage` |
| `Body` | Text, form labels (`Ng.FormField`), empty-state messages (`Ng.EmptyState`), note text and dates | `Ng.FormField`, `Ng.EmptyState` |
| `Caption` | Hints, validation messages, metadata, setting descriptions (`Ng.SettingHeader`) and grade sheet column sub-headers | `StudentPage` validation text |

Secondary text sets `Appearance="Secondary"`. This is the usual look of a hint or metadata. The page title uses the
primary colour.

### Tabular figures

Numbers that must line up use tabular figures: `Typography.NumeralAlignment="Tabular"`. The views set it in three
places:

- `Ng.DataGridText.Numeric`, the element style of a numeric data grid column. It also right-aligns the text.
- The subjects list of the student's Grades tab (`StudentPage`).
- The grade sheet grid (`GradeSheetPage`).

Grades, averages and counts are the numbers this applies to. A new numeric column uses `Ng.DataGridText.Numeric`.

### Known exceptions to "never FontSize"

- `Ng.PageRoot` sets `TextElement.FontSize` to the WPF-UI content size (`ControlContentThemeFontSize`). It is a
  theme value, not a literal.
- The in-cell marks of the grade sheet set `FontSize="14"` on their icon. See [Known gaps](#known-gaps).

## Semantic colours

All colours are theme brushes, used through `DynamicResource`. No XAML file in the App contains a hex colour; the
`DesignSystemRulesTests` check fails for one.

| Meaning | Brush | Also shown by |
|---|---|---|
| Default text | `TextFillColorPrimaryBrush` | Nothing extra |
| Hints and metadata | `TextFillColorSecondaryBrush` | The `Caption` step |
| Disabled, voided, not enrolled or exempt | `TextFillColorDisabledBrush` with `SubtleFillColorSecondaryBrush` as the cell fill | A symbol (`Subtract16`, `Prohibited16`, `DismissCircle16`), italic text for a row of an exempt or left student, the "Voided" caption, and the 0.6 opacity of a voided grade on the student page |
| Error | `SystemFillColorCriticalBrush` | A 2-pixel border and an `ErrorCircle16` symbol on an invalid grade cell. The cell tooltip gives the reason. |
| Caution | `SystemFillColorCautionBrush` | The "Exempt" badge on the student page. `Ng.Icon.Caution` colours a `Warning24` icon in front of a failed close on the school year page. |
| Success | `SystemFillColorSuccessBrush` | `Ng.Icon.Success` colours a `CheckmarkCircle24` icon: the "done" state and the "nothing is blocking the close" line on the school year page |
| Closed or unlocked year | A `ui:InfoBar` in `YearLockBar` | A lock icon and text |
| Keyboard focus | `AccentFillColorDefaultBrush` | A 2-pixel border on a grade cell |
| Selected grade cell | `ControlFillColorSecondaryBrush` | The cell fill |
| Lines and borders | `ControlStrokeColorDefaultBrush` | Borders of cards and item surfaces |
| Card and item background | `CardBackgroundFillColorSecondaryBrush` | `Ng.Surface.Item` |

Rules that follow from the table:

- **Colour is for states only.** Colour marks an error, a voided item, a closed year or a disabled item. It is never
  the only signal: each state also has a text or an icon.
- **Grades are never coloured by value.** The scale is lower-is-better, so no colour, bar or icon implies that a
  higher value is better. A grade shows as its token, such as "2-".
- **Accent carries no meaning.** The App sets no accent colour of its own. The accent brushes come from the WPF-UI
  theme. They mark the primary button and the keyboard focus, and nothing else.

## Page skeleton

Every page root is a `Grid` with the `Ng.PageRoot` style. The style sets the 24 page margin and the font face. The
root is never a `StackPanel`, because a `DataGrid` in a `StackPanel` gets unlimited height and loses its scroll and
virtualization.

Rows, top to bottom:

1. The header, `Auto`. A `HeaderedContentControl` with `Ng.PageHeader`.
2. The action bar, `Auto`. A `WrapPanel` with `Ng.ActionBar`. The main action comes first and uses
   `Appearance="Primary"`.
3. Optional status rows, `Auto`. For example, the year lock bar.
4. The content, `*`. A grid, a form in a `ScrollViewer`, or a list.

The `Ng.PageRoot` comment describes a five-row layout with a status row and a footer. The pages use three to six
rows, and the content always sits in the `*` row. See [Known gaps](#known-gaps).

```xml
<Grid Style="{StaticResource Ng.PageRoot}">
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto" />
        <RowDefinition Height="Auto" />
        <RowDefinition Height="*" />
    </Grid.RowDefinitions>

    <HeaderedContentControl Grid.Row="0"
                            Header="{Binding ViewModel.Title}"
                            Style="{StaticResource Ng.PageHeader}" />

    <WrapPanel Grid.Row="1" Style="{StaticResource Ng.ActionBar}">
        <ui:Button Margin="{StaticResource Ng.Margin.ActionItem}"
                   Appearance="Primary"
                   IsDefault="True"
                   Content="Save"
                   Icon="{ui:SymbolIcon Save24}"
                   Command="{Binding ViewModel.SaveCommand}" />
        <ui:Button Margin="{StaticResource Ng.Margin.ActionItem}"
                   IsCancel="True"
                   Content="Cancel"
                   Icon="{ui:SymbolIcon Dismiss24}"
                   Command="{Binding ViewModel.CancelCommand}" />
    </WrapPanel>

    <ScrollViewer Grid.Row="2" VerticalScrollBarVisibility="Auto">
        <!-- The form or the sections -->
    </ScrollViewer>
</Grid>
```

`PageHeader` takes an optional caption in its `Content`. The grade sheet uses it for the school year, as a
`ui:TextBlock` with `FontTypography="Body"` and `Appearance="Secondary"`. A header without a caption passes no
content.

## Shared styles

All keys are defined in `Styles.xaml`.

| Key | Target | What it does | Notes |
|---|---|---|---|
| `Ng.PageRoot` | `Grid` | The page margin, the font face and the body font size | The root of every page |
| `Ng.PageHeader` | `HeaderedContentControl` | The page title in `Title`, and an optional caption | Not a tab stop |
| `Ng.ActionBar` | `WrapPanel` | A horizontal row of buttons with the action bar margin | Buttons inside use `Ng.Margin.ActionItem` |
| `Ng.Card` | `ui:Card` | 16 padding and a 16 bottom margin | Groups related fields |
| `Ng.Card.Tile` | `ui:Card` | A fixed-width card (320) with the `Ng.Margin.Tile` margin (16 right and bottom) | Extends `Ng.Card`. The grade sheet uses it as a base. |
| `Ng.Section` | `HeaderedContentControl` | A `Subtitle` heading above the content, with a 24 bottom margin | Not a tab stop |
| `Ng.FormPanel` | `ds:FormPanel` (a `StackPanel`, `NexGrades.App/DesignSystem/FormPanel.cs`) | Fills the width it gets up to `Ng.Size.FormMaxWidth` (480), left aligned, and shrinks below that | A plain left-aligned `StackPanel` shrinks to its content (a tiny text box) and a fixed width overflows narrow containers, so `FormPanel` measures against the smaller of the container width and 480. Use `<ds:FormPanel Style="{StaticResource Ng.FormPanel}">` with `xmlns:ds="clr-namespace:NexGrades.App.DesignSystem"` |
| `Ng.FormField` | `HeaderedContentControl` | A `Body` label above the field, with a 16 bottom margin | The label is not a tab stop; the field inside is |
| `Ng.TextBox.Multiline` | `ui:TextBox` | Wraps, accepts Enter, scrolls, and is 72 to 120 high | Used for comments and notes |
| `Ng.EmptyState` | `ContentControl` | A secondary `Body` message | Not a tab stop |
| `Ng.EmptyState.WhenNoItems` | `ContentControl` | Shows the message while the collection in `Tag` is empty | Extends `Ng.EmptyState` |
| `Ng.DataGrid` | `ui:DataGrid` | Read-only, single row selection, 32 row height, no add, delete or row resizing (columns can still be resized) | Overview grids and the grade sheet extend it |
| `Ng.DataGridText` | `TextBlock` (element style) | Horizontal margin `Ng.Margin.CellText`, vertically centred | For every `DataGridTextColumn`; the library margin otherwise makes rows taller than 32 |
| `Ng.DataGridText.Numeric` | `TextBlock` (element style) | Extends `Ng.DataGridText`; right aligned, with tabular figures | For numeric columns |
| `Ng.Surface.Item` | `Border` | A bordered item surface with 16 padding and a corner radius of 4 | Used by a school year row |
| `Ng.SettingsCard` | `ui:CardControl` | An 8 bottom margin | Settings cards |
| `Ng.SettingHeader` | `HeaderedContentControl` | A setting name in `BodyStrong`, with an optional `Caption` description | Used as `CardControl.Header` |
| `Ng.Icon.Success`, `Ng.Icon.Caution`, `Ng.Icon.Critical` | `ui:SymbolIcon` | The foreground of a status icon | `Ng.Icon.Critical` is defined, but no view uses it yet |

Short usage examples:

```xml
<HeaderedContentControl Header="First name"
                        Style="{StaticResource Ng.FormField}">
    <ui:TextBox AutomationProperties.Name="First name"
                Text="{Binding ViewModel.FirstName, UpdateSourceTrigger=PropertyChanged}" />
</HeaderedContentControl>
```

```xml
<ContentControl Tag="{Binding ViewModel.Classes}"
                Content="No classes yet. Use Add class to create one."
                Style="{StaticResource Ng.EmptyState.WhenNoItems}" />
```

The `Tag` binding is the collection itself. The empty state reads `Tag.Count` through a `RelativeSource`, so the
message appears when the collection has no items.

## Data grids

- An overview grid uses `ui:DataGrid` with `Ng.DataGrid`, which sets the read-only, single-row and 32-pixel
  behaviour. Columns have explicit headers in real text.
- Every text column uses `ElementStyle="{StaticResource Ng.DataGridText}"`, so the library text margin does not
  make rows taller than 32. A numeric column uses `Ng.DataGridText.Numeric` instead.
- The grid sits in a `*` row, so it scrolls and stays virtualized.
- An empty overview uses `Ng.EmptyState.WhenNoItems` above the grid, with a message that names the action that fills
  the list.
- The grade sheet is the exception. It is editable, selects single cells and shows marks in its cells. It
  overrides `Ng.DataGrid` locally, and its cells use `GradeSheetCellBase`, which applies `Ng.Padding.Cell` and
  `Ng.Size.RowHeight`. These grade cell styles stay on the page. They are not shared.

```xml
<ui:DataGrid Style="{StaticResource Ng.DataGrid}"
             ItemsSource="{Binding ViewModel.Classes}"
             SelectedItem="{Binding ViewModel.SelectedClass}">
    <ui:DataGrid.Columns>
        <DataGridTextColumn Header="Name" Binding="{Binding Name}" />
        <DataGridTextColumn Header="Students" Binding="{Binding StudentCount}"
                            ElementStyle="{StaticResource Ng.DataGridText.Numeric}" />
    </ui:DataGrid.Columns>
</ui:DataGrid>
```

## Icons

Icons are `ui:SymbolIcon` from `SymbolRegular`. Use the 24 variant for buttons and page icons. The in-cell marks of
the grade sheet are the only exception: they use the 16 variant with `FontSize` 14 on the icon.

One icon stands for one meaning across the app. The table lists the icons in use.

| Meaning | Icon | Where it is used |
|---|---|---|
| Add | `Add24` | "Add" buttons on the overview pages, add grade, add note |
| Edit | `Edit24` | Edit class, student, subject, grade and note |
| Save | `Save24` | Save buttons |
| Cancel, or close an editor | `Dismiss24` | Cancel buttons |
| Void (reversible) | `DismissCircle24` | Void grade and assessment; `DismissCircle16` in the grade sheet |
| Restore | `ArrowUndo24` | Restore grade, note and assessment; `ArrowUndo16` in the grade sheet |
| Archive | `Archive24` | Archive note |
| Back | `ArrowLeft24` | Back buttons |
| Teacher's final grade override | `Pin24` on the student page, `Pin16` in the grade sheet | The override marker |
| Exempt | `Prohibited16` | The grade sheet cell of an exempt student |
| Unlocked or closed year | `LockOpen24`, `LockClosed24` | `YearLockBar` and the school year page |
| Warning, or a failed step | `Warning24` | The failed close on the school year page |
| Done, or nothing blocking | `CheckmarkCircle24` | The done state and the result of a close on the school year page |
| Open the grade sheet | `Table24` | The "Grades" button on the class page |
| Export | `ArrowExportUp24` (subject overview), `ArrowDownload24` (notes) | See [Known gaps](#known-gaps) |
| Students, Settings (navigation) | `People24`, `Settings24` | The sidebar |
| Settings items | `Folder24`, `FolderOpen24`, `DocumentText24`, `PaintBrush24`, `ArrowSync24` | `SettingsPage` |

An icon-only button needs `AutomationProperties.Name` and a `ToolTip`, as the edit and void buttons of a grade on
the student page have.

## Empty, loading and status states

- **Empty list.** Use `Ng.EmptyState` or `Ng.EmptyState.WhenNoItems`. The message is a whole sentence that names the
  action that fills the list, for example "No classes yet. Use Add class to create one."
- **Loading.** Use an indeterminate `ui:ProgressRing` with an `AutomationProperties.Name` that says what loads, for
  example "Loading grades". It sits next to the action bar or in the content. The loading state does not yet
  disable the action bar on every page (see [Known gaps](#known-gaps)).
- **Problem that stays on the page.** Use a `ui:InfoBar` at the top of the content. The closed-year banner in
  `YearLockBar` is one. The grade sheet shows an informational `InfoBar` when no sheet is available.
- **Validation.** When Save is disabled, a `Caption` `Secondary` text next to the field says why, as on the student
  and class pages.
- **Result of an action.** A snackbar, shown through `ISnackbarService`, as the view models do. See
  [AGENTS.md](../../AGENTS.md).
- **Confirmation.** A content dialog. Leaving a page with unsaved input uses the shared dialog in
  [navigation and leave guards](navigation-and-leave-guards.md).

## Tests and checks

| Check | What it enforces |
|---|---|
| `XamlLoadTests` (`NexGrades.App.Tests/Composition`) | Creates every page and the shared views on an STA thread, with every resource read. A view that uses an unmerged key, or a `DynamicResource` in `BasedOn`, fails. Keys used only inside item, data or cell templates are not instantiated by the test, so they are not checked. |
| `XamlSourceRulesTests` (`NexGrades.App.Tests/Composition`) | Scans every App XAML file. It fails for a style that sets `Visibility` on a `ui:TextBlock`, and for a page title that equals its class name. It fails if no `ui:TextBlock` is found, so the rule cannot pass without checking anything. It also fails for a file with an `IsDefault` or `IsCancel` button that has a `TextBox` whose `Text` binding lacks `UpdateSourceTrigger=PropertyChanged` (Enter and Escape would act on a stale value). |
| `DesignSystemRulesTests` (`NexGrades.App.Tests/Composition`) | Scans every App XAML file outside `obj` and `bin`. It fails for three rules: a view (`Page`, `UserControl`, `ContentDialog` or `FluentWindow` root) that does not merge `DesignSystem.xaml` in its resources; a `Margin` or `Padding`, as an attribute, a setter or a `Thickness` resource, whose value is not a multiple of 4; and a hex colour in any attribute value or text. A markup extension such as `{StaticResource ...}` is not checked for spacing. |
| `GradeSheetColumnHeaderTemplateTests` | The "Type 1–6 to override" hint shows only on the final grade column. |

`DesignSystemRulesTests` does not check which tokens a view uses, which brushes or named colours it uses, or any
`FontSize`. Review covers those.

The App is a GUI, so a change to a view also needs a manual check. From `src/NexGrades/`:

```sh
dotnet build NexGrades.slnx -warnaserror
dotnet test
dotnet run --project Aspire/NexGrades.AppHost
```

Then, in the running app:

1. Open Settings and set the theme to Light. Visit each page. Set it to Dark. Visit each page again. Set it to System.
2. Check that text is legible, that icons show, and that every state (voided, exempt, error, closed year) also shows
   text or an icon, not only a colour.
3. Resize the window to about 900 × 600. Nothing should be clipped, and the grids and panels should scroll.
4. Use the keyboard. The tab order should be logical, Enter saves a grade and Escape cancels it.

State in the pull request that the manual check was done.

## When to add a token or a style

Add a **token** to `Tokens.xaml` when a value is a spacing, size, radius or font face and a second view needs it.
The file's comment says to add the token before you use a new literal in a view. Keep the naming pattern:
`Ng.Margin.*`, `Ng.Padding.*`, `Ng.Size.*`, `Ng.Radius.*`.

Add a **style** to `Styles.xaml` when the same setters appear on more than one element or in more than one view.
Keep a style that serves one page in that page's own resources, with the page's name as a prefix, as the grade
sheet does.

Whatever you add:

- Use the `Ng.` prefix for a shared key. Page-local keys have no prefix.
- Do not add a style that targets `ui:TextBlock`.
- Do not use `DynamicResource` in `BasedOn`.
- Do not write a hex colour or a `FontSize`. Use a theme brush and a `FontTypography` step. A hex colour fails
  `DesignSystemRulesTests`.
- Do not rename a shared key without changing every view that uses it. A missing key fails when the view loads.
- After you add a token or a style, run `dotnet test` and check the page in Light and Dark.

## Known gaps

The rules above are the target. These places do not follow them yet:

1. `GradeSheetPage.xaml` sets `FontSize="14"` on its in-cell marks. The marks use the 16 glyph variant.
2. Several views still use literal margins and paddings, which are multiples of 4, instead of the tokens.
   `DesignSystemRulesTests` accepts them, because it checks only the multiple of 4. Examples:
   `StudentPage.xaml`, `SchoolYearPage.xaml`, `SettingsPage.xaml`, `GradeSheetPage.xaml`. New and touched code uses
   the tokens.
3. `Ng.Space.*` and `Ng.Icon.Critical` are defined, and no view uses them.
4. The two export actions use different icons: `ArrowExportUp24` for the subject overview and `ArrowDownload24` for
   the notes export. One icon per meaning would make them the same.
5. The comment on `Ng.PageRoot` describes a five-row page. The pages use three to six rows, with the content in the
   `*` row.
6. The action bars of the grade sheet and other pages do not all disable while a load runs.
7. The overview pages show their `Ng.EmptyState.WhenNoItems` message briefly before the list loads, because the
   collections start empty and are filled when the page loads. The view models expose no loaded flag to wait for.

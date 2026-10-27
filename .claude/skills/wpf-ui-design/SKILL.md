---
name: wpf-ui-design
description: UI design rules for NexGrades (WPF + WPF-UI 4 Fluent), covering page structure, layout and spacing, typography, controls, theming, feedback, grade display, closed-year states, localization readiness and accessibility. Use when designing, building or reviewing any XAML page or dialog.
---

# NexGrades UI design rules

NexGrades is a WPF-UI 4 `FluentWindow` with a left `NavigationView`, Mica backdrop and the Dark theme by
default (`App.xaml`). New UI must look like it belongs to that app and must work in both Light and Dark.

## Page structure

Every page follows the same skeleton, top to bottom:

1. **Title**: `ui:TextBlock FontTypography="Title"`, the page's name as real text (e.g. "Classes").
2. **Action bar**: a horizontal row of `ui:Button`s with icons. The main action comes first and uses
   `Appearance="Primary"`; the others keep the default appearance.
3. **Content**: grid, form or detail view.

Use a `Grid` with `RowDefinitions` (`Auto`, `Auto`, `*`) as the page root, not a `StackPanel`. A
`DataGrid` inside a `StackPanel` gets unlimited height, so it can't scroll and loses virtualization.

## Layout and spacing

- Use multiples of 4 for every margin and padding: 4, 8, 12, 16, 24. Use 24 for the page padding, 12
  between sections, 8 between related controls and 4 inside groups. Existing pages still use 5; move
  them to this scale when you touch them.
- Forms are one column, at most about 480 px wide, with the label above the field. A `PlaceholderText` is
  a hint, not a label.
- Detail pages for one entity (student, class, subject) use `ui:Card` or `ui:CardExpander` to group
  related fields.
- Layouts must survive the window resized to about 900 × 600 without clipping.

## Typography

Use only `FontTypography` values and don't set `FontSize` by hand:

- `Title` for page titles
- `Subtitle` for sections
- `BodyStrong` for emphasis and column headers in custom layouts
- `Body` for text
- `Caption` for hints and metadata

Secondary text uses `Appearance="Secondary"`.

## Controls

- Prefer the WPF-UI controls: `ui:Button`, `ui:TextBox`, `ui:NumberBox`, `ui:AutoSuggestBox`,
  `ui:DataGrid`, `ui:Card`, `ui:InfoBar`, `ui:ToggleSwitch`, `ComboBox`, `DatePicker`.
- Icons are `ui:SymbolIcon` from `SymbolRegular`, size 24 for new work (`Add24`, `Edit24`, `Save24`,
  `Archive24`), one icon per meaning across the app.
- Overview grids: `ui:DataGrid` with `AutoGenerateColumns="False"`, `IsReadOnly="True"`,
  `SelectionMode="Single"` and explicit column headers in real text. Double-click or Enter on a row
  opens its detail page.
- Use `ui:NumberBox` for numeric input like weights. Grades are picked from the 16 legal values
  (`GradingScale.LegalValues`, shown as tokens), not typed as free numbers.

## Destructive actions: there are none

The domain never deletes rows. A button that looks like "Remove" or "Delete" must instead trigger a status
change and be labelled for what it does: "Archive class", "End enrollment", "Deactivate subject". Ask
for confirmation in a dialog that says what happens and that it can be undone.

## Theming and color

- Use theme brushes only, through `DynamicResource`: `TextFillColorPrimaryBrush`,
  `TextFillColorSecondaryBrush`, `CardBackgroundFillColorDefaultBrush`, `ControlStrokeColorDefaultBrush`,
  `SystemFillColorCriticalBrush` and so on. No hex colors.
- Never use color as the only signal (e.g. for failing grades); pair it with text or an icon.

## Feedback

- Results of an action: `ISnackbarService.Show(...)` with `ControlAppearance.Success` or `Caution` and an
  icon, about 5 seconds, as the existing view models do. The message is a whole sentence ("Class has been
  saved.").
- Problems that persist on the page (no active school year, closed year, invalid weights): a `ui:InfoBar`
  at the top of the content.
- Confirmations: a content dialog. Register `IContentDialogService` in `HostBuilderExtensions` the first
  time one is needed.
- Long loads: show a `ui:ProgressRing` and disable the action bar while they run.
- Empty lists show a short message and the action that fills them ("No classes yet. Add a class.").

## Forms and validation

- Disable Save until the input is valid, through the command's `CanExecute`, and show the reason next to the
  field in `Caption` text.
- Cancel navigates back without saving. If there are unsaved changes, ask first.

## Grades and averages

- Show grades as tokens via `GradingScale.TokenForValue` ("2-"), never as raw decimals.
- Show averages with two decimals in the current UI culture (`2.35` in English, `2,35` in German). Show
  "—" when there is no average, never `0`.
- Lower is better on this scale. Don't use visuals that imply higher is better, such as bars that grow
  with the value.

## Closed school years

When the selected year is Closed and not unlocked, pages show its data read-only:

- an `ui:InfoBar` explaining that the year is closed;
- disabled edit actions;
- an explicit "Unlock year" action that asks for a reason.

## Localization readiness

The UI is English now, and German comes next.

- Write all text as whole phrases. No sentences assembled in code or XAML from pieces.
- Allow about 30% extra width for German text. No fixed-width buttons or columns that only fit English.
- Format numbers and dates with the current culture. Never hard-code a decimal separator.

## Accessibility

- Everything is reachable by keyboard in a logical tab order. The main action has `IsDefault="True"`
  and Cancel has `IsCancel="True"`.
- Icon-only buttons need `AutomationProperties.Name` and a `ToolTip`.
- Don't convey meaning through color alone.

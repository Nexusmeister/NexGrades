---
name: new-feature-page
description: Adds a new page to the NexGrades WPF app with the project's pattern, covering the XAML page, code-behind, view model, DI registration and navigation entry or hand-off. Use when a new screen, overview, detail or edit page is needed.
argument-hint: <Area> <PageName> (e.g. Grades GradeEntry)
---

# Add a feature page

Follow the existing pattern exactly; `Features/Classes/` is the reference implementation. Apply the
design rules in `.claude/skills/wpf-ui-design/SKILL.md` to the XAML.

## 1. Decide the page kind

- **Top-level overview**, reachable from the left menu: registered as a **singleton** and gets a
  `NavigationViewItem` in the shell.
- **Detail or edit page**, opened from another page: registered as **transient**, with the transient view
  model reached through `NavigateWithHierarchy`.

## 2. Create the files in `src/NexGrades/NexGrades.App/Features/<Area>/`

**`<Name>ViewModel.cs`**

```csharp
namespace NexGrades.App.Features.<Area>;

public partial class <Name>ViewModel(
    INavigationService navigation,
    ISnackbarService snackbar,
    IDbContextFactory<AppDbContext> dbContextFactory) : ViewModel
{
    [ObservableProperty] private string _title = "<Real page title>";

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        // Query with AsNoTracking() and project into a record, e.g. `public sealed record <Name>Row(...)`.
    }
}
```

Business rules go in a Domain service injected here, not in the view model.

**`<Name>Page.xaml.cs`**

```csharp
using Wpf.Ui.Abstractions.Controls;

namespace NexGrades.App.Features.<Area>;

public partial class <Name>Page : INavigableView<<Name>ViewModel>
{
    public <Name>Page(<Name>ViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }

    public <Name>ViewModel ViewModel { get; }
}
```

**`<Name>Page.xaml`**: a `Page` with the `ui` namespace (`http://schemas.lepo.co/wpfui/2022/xaml`) and
`d:DataContext="{d:DesignInstance <area>:<Name>Page, IsDesignTimeCreatable=False}"`. Bind through
`{Binding ViewModel.X}`. Load data either in an override of `OnNavigatedToAsync` in the view model or with
the `Loaded` → `InvokeCommandAction` trigger used in `ClassesOverviewPage.xaml`.

## 3. Register in `NexGrades.App/Extensions/HostBuilderExtensions.cs`

- In `AddViews`, add the page: `.AddSingleton<<Name>Page>()` for an overview or `.AddTransient<<Name>Page>()`
  for a detail page.
- In `AddViewModels`, add the view model with the same lifetime.
- Register any new Domain service in `AddGradeServices`.

## 4. Wire up navigation

- **Overview page:** add a `ui:NavigationViewItem` with real text and a `SymbolIcon` (size 24) to
  `MenuItems` in `NexGrades.App/Shell/MainWindow.xaml`, with `TargetPageType="{x:Type <area>:<Name>Page}"`.
  Add the `xmlns` for the area if it's missing.
- **Detail or edit page:** navigate with `navigation.NavigateWithHierarchy(typeof(<Name>Page))`. To hand an
  id to a transient view model, use a small singleton request object, as `ClassEditRequest` does: the
  caller sets the id, and the target reads and clears it in `OnNavigatedToAsync`.

## 5. Verify

From `src/NexGrades/`, run `dotnet build NexGrades.App/NexGrades.App.csproj` and `dotnet test`. Then tell the
user what to check by hand: the page opens from its entry point, data loads, both themes look right, and
the Back button returns to the right place.

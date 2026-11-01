---
name: new-feature-page
description: Adds a new page to the NexGrades WPF app with the project's pattern, covering the XAML page, code-behind, view model, DI registration and navigation entry or hand-off. Use when a new screen, overview, detail or edit page is needed.
argument-hint: <Area> <PageName> (e.g. Grades GradeEntry)
---

# Add a feature page

Follow the existing pattern exactly; `Features/Classes/` is the reference implementation. Apply the
design rules in `.claude/skills/wpf-ui-design/SKILL.md` to the XAML.

## 1. Decide the page kind

- **Top-level overview**, reachable from the left menu: page and view model are both **singletons**, the
  page gets a `NavigationViewItem` in the shell, and it is added to `_rootPages` in `PageLifetimeTests`.
- **Add, edit or detail page**, opened from another page: page and view model are both **transient**, and
  it is opened with `PageNavigator`.

The page and its view model always share one lifetime. Mixing them is a bug (see `AGENTS.md`, "Page lifetimes
and navigation").

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

For an add, edit or detail page, also override `OnNavigatedToAsync` to receive the parameter. It is `null` when
the page is opened without one (the Add flow), so reset to the add state in that case:

```csharp
protected override async Task OnNavigatedToAsync(object? parameter)
{
    _editingId = parameter as int?;
    // Reset the form to the add state, then load the record when _editingId is set.
}
```

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

- Choose the lifetime from step 1 and register the pair with it. For an overview, use `AddSingleton` for both.
  For an add, edit or detail page, use `AddTransient` for both.
- In `AddViews`, add the page: `.AddSingleton<<Name>Page>()` or `.AddTransient<<Name>Page>()`.
- In `AddViewModels`, add the view model with the same lifetime: `.AddSingleton<<Name>ViewModel>()` or
  `.AddTransient<<Name>ViewModel>()`.
- Register any new Domain service in `AddGradeServices`.

## 4. Wire up navigation

- **Overview page:** add a `ui:NavigationViewItem` with real text and a `SymbolIcon` (size 24) to
  `MenuItems` in `NexGrades.App/Shell/MainWindow.xaml`, with `TargetPageType="{x:Type <area>:<Name>Page}"`.
  Add the `xmlns` for the area if it's missing.
- **Add, edit or detail page:** navigate forward with the injected `PageNavigator`, not `INavigationService`:
  `navigator.NavigateWithHierarchy<<Name>Page>(id)` to open an existing record, or
  `navigator.NavigateWithHierarchy<<Name>Page>()` for the Add flow. The target view model reads the value in
  `OnNavigatedToAsync` (step 2). Do not add a singleton request or mailbox class for this.

## 5. Verify

From `src/NexGrades/`, run `dotnet build NexGrades.App/NexGrades.App.csproj` and `dotnet test`. Check by hand that
the page and view model are registered with the same lifetime. `PageLifetimeTests` in `NexGrades.App.Tests`
checks this, but only for pages that implement `INavigableView<TViewModel>`, so the page must implement it. Then tell the
user what to check by hand: the page opens from its entry point, data loads, both themes look right, and
the Back button returns to the right place.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace NexGrades.App.Features.Grades;

/// <summary>
/// Attached behavior that gives a <see cref="DataGrid"/> bindable columns, which <c>DataGrid.Columns</c> is not.
/// It rebuilds the columns from an <see cref="ObservableCollection{T}"/> of <see cref="GradeSheetColumn"/>
/// whenever the collection is replaced or changes, freezes the first (student) column, binds each assessment
/// column to its cell by index, cancels editing of cells that are not editable, and reports the grid's current
/// cell through <see cref="SelectedCellProperty"/>.
/// </summary>
/// <remarks>
/// A page styles the result with resources found from the grid, so its own XAML owns the look in both themes.
/// <see cref="GradeCellStyleKey"/> is an optional <c>DataGridCell</c> style the behavior extends for each grade
/// column: it adds a <c>Tag</c> bound to the cell's <see cref="GradeCellViewModel.VisualState"/> and a tooltip
/// bound to its <see cref="GradeCellViewModel.Description"/>, so the base style can react to the state with
/// triggers on <c>Tag</c>. <see cref="ReadOnlyCellStyleKey"/> styles the other columns and
/// <see cref="HeaderTemplateKey"/> is a <c>DataTemplate</c> whose data is the <see cref="GradeSheetColumn"/>.
/// Missing resources are skipped.
/// </remarks>
public static class DataGridColumnsBehavior
{
    /// <summary>Resource key of the <c>DataGridCell</c> style used for assessment (grade) columns.</summary>
    public const string GradeCellStyleKey = "GradeSheetGradeCellStyle";

    /// <summary>Resource key of the <c>DataGridCell</c> style used for every other column.</summary>
    public const string ReadOnlyCellStyleKey = "GradeSheetReadOnlyCellStyle";

    /// <summary>Resource key of the header <c>DataTemplate</c>; its data is the <see cref="GradeSheetColumn"/>.</summary>
    public const string HeaderTemplateKey = "GradeSheetColumnHeaderTemplate";

    /// <summary>The columns to show; the grid's columns are rebuilt from them.</summary>
    public static readonly DependencyProperty ColumnsProperty = DependencyProperty.RegisterAttached(
        "Columns",
        typeof(ObservableCollection<GradeSheetColumn>),
        typeof(DataGridColumnsBehavior),
        new PropertyMetadata(null, OnColumnsChanged));

    /// <summary>
    /// The grade cell the grid's current cell is on, or <see langword="null"/> when it is on a non-grade column.
    /// Bind it <c>OneWayToSource</c> to the view model's <c>SelectedCell</c>.
    /// </summary>
    public static readonly DependencyProperty SelectedCellProperty = DependencyProperty.RegisterAttached(
        "SelectedCell",
        typeof(GradeCellViewModel),
        typeof(DataGridColumnsBehavior),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    // Remembers the sheet column behind each grid column, and the collection handler of each grid.
    private static readonly DependencyProperty _sheetColumnProperty = DependencyProperty.RegisterAttached(
        "SheetColumn", typeof(GradeSheetColumn), typeof(DataGridColumnsBehavior));

    private static readonly DependencyProperty _subscriptionProperty = DependencyProperty.RegisterAttached(
        "Subscription", typeof(Subscription), typeof(DataGridColumnsBehavior));

    /// <summary>Gets the columns of <paramref name="grid"/>.</summary>
    /// <param name="grid">The grid.</param>
    /// <returns>The column collection, or null.</returns>
    public static ObservableCollection<GradeSheetColumn>? GetColumns(DependencyObject grid) =>
        (ObservableCollection<GradeSheetColumn>?)grid.GetValue(ColumnsProperty);

    /// <summary>Sets the columns of <paramref name="grid"/>.</summary>
    /// <param name="grid">The grid.</param>
    /// <param name="value">The column collection.</param>
    public static void SetColumns(DependencyObject grid, ObservableCollection<GradeSheetColumn>? value) =>
        grid.SetValue(ColumnsProperty, value);

    /// <summary>Gets the grade cell the grid's current cell is on.</summary>
    /// <param name="grid">The grid.</param>
    /// <returns>The cell, or null.</returns>
    public static GradeCellViewModel? GetSelectedCell(DependencyObject grid) =>
        (GradeCellViewModel?)grid.GetValue(SelectedCellProperty);

    /// <summary>Sets the grade cell the grid's current cell is on.</summary>
    /// <param name="grid">The grid.</param>
    /// <param name="value">The cell.</param>
    public static void SetSelectedCell(DependencyObject grid, GradeCellViewModel? value) =>
        grid.SetValue(SelectedCellProperty, value);

    private static void OnColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid)
        {
            return;
        }

        if (grid.GetValue(_subscriptionProperty) is Subscription old)
        {
            old.Detach();
        }

        if (e.NewValue is ObservableCollection<GradeSheetColumn> columns)
        {
            var subscription = new Subscription(grid, columns);
            subscription.Attach();
            grid.SetValue(_subscriptionProperty, subscription);
        }
        else
        {
            grid.ClearValue(_subscriptionProperty);
        }

        Rebuild(grid);
    }

    private static void Rebuild(DataGrid grid)
    {
        grid.Columns.Clear();
        var columns = GetColumns(grid);
        if (columns is null)
        {
            return;
        }

        var headerTemplate = grid.TryFindResource(HeaderTemplateKey) as DataTemplate;
        var gradeStyle = grid.TryFindResource(GradeCellStyleKey) as Style;
        var readOnlyStyle = grid.TryFindResource(ReadOnlyCellStyleKey) as Style;

        foreach (var column in columns)
        {
            var gridColumn = new DataGridTextColumn
            {
                Binding = new Binding(column.BindingPath) { Mode = column.IsEditable ? BindingMode.TwoWay : BindingMode.OneWay },
                IsReadOnly = !column.IsEditable,
                CanUserSort = false,
                CanUserReorder = false,
                CellStyle = column.IsEditable ? CreateGradeCellStyle(gradeStyle, column) : readOnlyStyle,
            };

            if (headerTemplate is not null)
            {
                gridColumn.Header = column;
                gridColumn.HeaderTemplate = headerTemplate;
            }
            else
            {
                gridColumn.Header = column.Header;
            }

            gridColumn.SetValue(_sheetColumnProperty, column);
            grid.Columns.Add(gridColumn);
        }

        // The student column stays in view while the assessments scroll.
        grid.FrozenColumnCount = columns.Count > 0 && columns[0].Kind == GradeSheetColumnKind.Student ? 1 : 0;
    }

    private static Style CreateGradeCellStyle(Style? baseStyle, GradeSheetColumn column)
    {
        var style = new Style(typeof(DataGridCell), baseStyle);
        style.Setters.Add(new Setter(FrameworkElement.TagProperty, new Binding(column.CellPath(nameof(GradeCellViewModel.VisualState)))));
        style.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, new Binding(column.CellPath(nameof(GradeCellViewModel.Description)))));
        return style;
    }

    private static void OnBeginningEdit(object? sender, DataGridBeginningEditEventArgs e)
    {
        if (e.Column.GetValue(_sheetColumnProperty) is not GradeSheetColumn { IsEditable: true } column
            || e.Row.Item is not GradeSheetRow row
            || column.CellIndex < 0
            || column.CellIndex >= row.Cells.Count
            || !row.Cells[column.CellIndex].IsEditable)
        {
            e.Cancel = true;
        }
    }

    private static void OnCurrentCellChanged(DataGrid grid)
    {
        var current = grid.CurrentCell;
        GradeCellViewModel? cell = null;
        if (current.Column?.GetValue(_sheetColumnProperty) is GradeSheetColumn { IsEditable: true } column
            && current.Item is GradeSheetRow row
            && column.CellIndex >= 0
            && column.CellIndex < row.Cells.Count)
        {
            cell = row.Cells[column.CellIndex];
        }

        if (!ReferenceEquals(GetSelectedCell(grid), cell))
        {
            SetSelectedCell(grid, cell);
        }
    }

    /// <summary>Keeps the grid's collection and event handlers wired up, and detaches them again.</summary>
    private sealed class Subscription(DataGrid grid, ObservableCollection<GradeSheetColumn> columns)
    {
        public void Attach()
        {
            columns.CollectionChanged += OnCollectionChanged;
            grid.BeginningEdit += OnBeginningEdit;
            grid.CurrentCellChanged += OnCurrentCell;
        }

        public void Detach()
        {
            columns.CollectionChanged -= OnCollectionChanged;
            grid.BeginningEdit -= OnBeginningEdit;
            grid.CurrentCellChanged -= OnCurrentCell;
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild(grid);

        private void OnCurrentCell(object? sender, EventArgs e) => OnCurrentCellChanged(grid);
    }
}

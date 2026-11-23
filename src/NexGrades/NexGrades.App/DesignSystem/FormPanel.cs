using System.Windows;
using System.Windows.Controls;

namespace NexGrades.App.DesignSystem;

/// <summary>
/// A vertical form column that fills the width it is given up to <see cref="MaxFormWidth"/> and shrinks below that.
/// A plain <see cref="StackPanel"/> aligned left shrinks to its content (so a text box ends up tiny), and a fixed
/// width overflows narrow containers; measuring against the smaller of the two avoids both.
/// </summary>
public sealed class FormPanel : StackPanel
{
    /// <summary>Identifies <see cref="MaxFormWidth"/>.</summary>
    public static readonly DependencyProperty MaxFormWidthProperty = DependencyProperty.Register(
        nameof(MaxFormWidth),
        typeof(double),
        typeof(FormPanel),
        new FrameworkPropertyMetadata(480d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>Gets or sets the widest the form column gets; the design system sets it from <c>Ng.Size.FormMaxWidth</c>.</summary>
    public double MaxFormWidth
    {
        get => (double)GetValue(MaxFormWidthProperty);
        set => SetValue(MaxFormWidthProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size constraint)
    {
        var width = Math.Min(constraint.Width, MaxFormWidth);
        var measured = base.MeasureOverride(new Size(width, constraint.Height));
        return new Size(width, measured.Height);
    }
}

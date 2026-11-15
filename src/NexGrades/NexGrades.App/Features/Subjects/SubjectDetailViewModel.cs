using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using NexGrades.App.Core;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace NexGrades.App.Features.Subjects;

/// <summary>Form for adding a subject to the catalogue.</summary>
/// <param name="navigation">Navigates back after a save or a cancel.</param>
/// <param name="snackbar">Reports the outcome of a save.</param>
/// <param name="subjectService">Validates and saves the subject.</param>
public partial class SubjectDetailViewModel(INavigationService navigation, ISnackbarService snackbar, SubjectService subjectService) : ViewModel
{
    [ObservableProperty] private string _title = "AddSubject";
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _shortCode = string.Empty;

    /// <summary>Longest subject name the form accepts; for the text box's <c>MaxLength</c> and a counter.</summary>
    public int MaxNameLength { get; } = SubjectService.MaxNameLength;

    /// <summary>Longest short code the form accepts; for the text box's <c>MaxLength</c> and a counter.</summary>
    public int MaxShortCodeLength { get; } = SubjectService.MaxShortCodeLength;

    [RelayCommand]
    private void Cancel()
    {
        navigation.GoBack();
    }

    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (Name.Trim().Length > MaxNameLength)
        {
            ShowError(string.Format(CultureInfo.CurrentCulture, "The subject name can be at most {0} characters.", MaxNameLength));
            return;
        }

        if (ShortCode.Trim().Length > MaxShortCodeLength)
        {
            ShowError(string.Format(CultureInfo.CurrentCulture, "The short code can be at most {0} characters.", MaxShortCodeLength));
            return;
        }

        try
        {
            await subjectService.CreateSubjectAsync(Name, ShortCode, cancellationToken);
        }
        catch (Exception ex) when (ex is InvariantViolationException or DbUpdateException)
        {
            snackbar.Show(
                "Error",
                string.Format(
                    CultureInfo.CurrentCulture,
                    "The subject could not be saved. Enter a name of up to {0} characters and a short code of up to {1} characters.",
                    SubjectService.MaxNameLength,
                    SubjectService.MaxShortCodeLength),
                ControlAppearance.Caution,
                new SymbolIcon(SymbolRegular.ErrorCircle24),
                TimeSpan.FromSeconds(5));
            return;
        }

        snackbar.Show("Success", "Subject has been saved.", ControlAppearance.Success, new SymbolIcon(SymbolRegular.CheckmarkCircle32), TimeSpan.FromSeconds(5));
        navigation.GoBack();
    }

    private void ShowError(string message) =>
        snackbar.Show("Error", message, ControlAppearance.Caution, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using NexGrades.App.Core;
using NexGrades.Data;
using NexGrades.Data.Entities;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace NexGrades.App.Features.Subjects;

public partial class SubjectDetailViewModel(INavigationService navigation, ISnackbarService snackbar, IDbContextFactory<AppDbContext> dbContext) : ViewModel
{
    [ObservableProperty] private string _title = "AddSubject";
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _shortCode = string.Empty;

    [RelayCommand]
    private void Cancel()
    {
        navigation.GoBack();
    }

    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var db = await dbContext.CreateDbContextAsync(cancellationToken);
        var subjectToAdd = new Subject
        {
            Name = Name,
            ShortCode = ShortCode,
        };

        db.Subjects.Add(subjectToAdd);
        var saved = await db.SaveChangesAsync(cancellationToken);

        if (saved != 0)
        {
            snackbar.Show("Success", "Subject has been saved", ControlAppearance.Success, new SymbolIcon(SymbolRegular.CheckmarkCircle32), TimeSpan.FromSeconds(5));
            navigation.GoBack();
        }
        else
        {
            snackbar.Show("Error", "Subject has not been saved", ControlAppearance.Caution, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));
        }
    }
}

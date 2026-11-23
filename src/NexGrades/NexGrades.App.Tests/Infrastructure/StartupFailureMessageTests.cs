using System.Globalization;
using System.IO;
using NexGrades.App.Infrastructure;
using NexGrades.App.Localization;

namespace NexGrades.App.Tests.Infrastructure;

public sealed class StartupFailureMessageTests
{
    private const string BackupPath = @"C:\Users\someone\AppData\Local\NexGrades\backups\app-20261115-103000-v0.1.0.db";
    private const string LogFolder = @"C:\Users\someone\AppData\Local\NexGrades\logs";
    private const string Secret = "secret-cause-Zebra42";

    [Fact]
    public void BuildMessage_UsesBackupFailedText_WithLogFolder()
    {
        var message = StartupFailureHandler.BuildMessage(new DatabaseBackupFailedException(new IOException(Secret)), LogFolder);

        Assert.Equal(TextResources.Format("Infrastructure_Startup_BackupFailed_WithLogFolder_Format", LogFolder), message);
        Assert.Contains("data is unchanged", message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void BuildMessage_UsesBackupFailedText_WithoutLogFolder(string? logFolder)
    {
        var message = StartupFailureHandler.BuildMessage(new DatabaseBackupFailedException(new IOException(Secret)), logFolder);

        Assert.Equal(TextResources.Text("Infrastructure_Startup_BackupFailed"), message);
    }

    [Fact]
    public void BuildMessage_NamesTheBackup_WhenTheMigrationFailedAndTheLogFolderIsKnown()
    {
        var message = StartupFailureHandler.BuildMessage(new DatabaseMigrationFailedException(BackupPath, new InvalidOperationException(Secret)), LogFolder);

        Assert.Equal(TextResources.Format("Infrastructure_Startup_MigrationFailed_WithLogFolder_Format", BackupPath, LogFolder), message);
        Assert.Contains(BackupPath, message, StringComparison.Ordinal);
        Assert.Contains(LogFolder, message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildMessage_NamesTheBackup_WhenTheMigrationFailedAndNoLogFolderIsKnown()
    {
        var message = StartupFailureHandler.BuildMessage(new DatabaseMigrationFailedException(BackupPath, new InvalidOperationException(Secret)), null);

        Assert.Equal(TextResources.Format("Infrastructure_Startup_MigrationFailed_Format", BackupPath), message);
        Assert.DoesNotContain(LogFolder, message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildMessage_UsesTheGenericText_ForAnyOtherFailure()
    {
        var withFolder = StartupFailureHandler.BuildMessage(new InvalidOperationException(Secret), LogFolder);
        var withoutFolder = StartupFailureHandler.BuildMessage(new InvalidOperationException(Secret), null);

        Assert.Equal(TextResources.Format("Infrastructure_Startup_Failed_WithLogFolder_Format", LogFolder), withFolder);
        Assert.Equal(TextResources.Text("Infrastructure_Startup_Failed"), withoutFolder);
    }

    [Fact]
    public void BuildMessage_IsGerman_ForGermanUiCulture()
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            var message = StartupFailureHandler.BuildMessage(new DatabaseMigrationFailedException(BackupPath, new IOException(Secret)), LogFolder);

            Assert.StartsWith("NexGrades konnte seine Datenbank nicht aktualisieren.", message, StringComparison.Ordinal);
            Assert.Contains(BackupPath, message, StringComparison.Ordinal);
            Assert.Contains(LogFolder, message, StringComparison.Ordinal);
            Assert.DoesNotContain(Secret, message, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void BuildMessage_ShowsTheBackupPathOnlyForAFailedMigration()
    {
        var backupFailed = StartupFailureHandler.BuildMessage(new DatabaseBackupFailedException(new IOException(BackupPath)), LogFolder);
        var generic = StartupFailureHandler.BuildMessage(new IOException(BackupPath), LogFolder);

        Assert.DoesNotContain(BackupPath, backupFailed, StringComparison.Ordinal);
        Assert.DoesNotContain(BackupPath, generic, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildMessage_NeverShowsExceptionText()
    {
        var messages = new[]
        {
            StartupFailureHandler.BuildMessage(new DatabaseBackupFailedException(new IOException(Secret)), LogFolder),
            StartupFailureHandler.BuildMessage(new DatabaseMigrationFailedException(BackupPath, new IOException(Secret)), LogFolder),
            StartupFailureHandler.BuildMessage(new IOException(Secret), LogFolder),
        };

        Assert.All(messages, m => Assert.DoesNotContain(Secret, m, StringComparison.Ordinal));
    }

    [Fact]
    public void Handle_ShowsTheMigrationFailedTextOnceAndExits_ForADatabaseMigrationFailedException()
    {
        var messages = new List<string>();
        var exitCodes = new List<int>();
        var handler = new StartupFailureHandler(new RecordingDialog(messages), exitCodes.Add);

        handler.Handle(new DatabaseMigrationFailedException(BackupPath, new IOException(Secret)), () => null, () => null, () => LogFolder);

        Assert.Equal([TextResources.Format("Infrastructure_Startup_MigrationFailed_WithLogFolder_Format", BackupPath, LogFolder)], messages);
        Assert.Equal([StartupFailureHandler.FailureExitCode], exitCodes);
    }

    [Fact]
    public void Constructor_KeepsConstantMessages_ThatNameNoPath()
    {
        var backup = new DatabaseBackupFailedException(new IOException(BackupPath));
        var migration = new DatabaseMigrationFailedException(BackupPath, new IOException(BackupPath));

        Assert.Equal(DatabaseBackupFailedException.DefaultMessage, backup.Message);
        Assert.Equal(DatabaseMigrationFailedException.DefaultMessage, migration.Message);
        Assert.Equal(BackupPath, migration.BackupFilePath);
    }

    private sealed class RecordingDialog(List<string> messages) : IStartupErrorDialog
    {
        public void Show(string message) => messages.Add(message);
    }
}

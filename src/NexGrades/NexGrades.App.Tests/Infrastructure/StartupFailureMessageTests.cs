using System.Globalization;
using System.IO;
using NexGrades.App.Infrastructure;

namespace NexGrades.App.Tests.Infrastructure;

public sealed class StartupFailureMessageTests
{
    private const string BackupPath = @"C:\Users\someone\AppData\Local\NexGrades\backups\app-20261115-103000-v0.1.0.db";
    private const string LogFolder = @"C:\Users\someone\AppData\Local\NexGrades\logs";
    private const string Secret = "secret-cause-Zebra42";

    private static string Format(string format, params object[] args) => string.Format(CultureInfo.CurrentCulture, format, args);

    [Fact]
    public void BuildMessage_UsesBackupFailedText_WithLogFolder()
    {
        var message = StartupFailureHandler.BuildMessage(new DatabaseBackupFailedException(new IOException(Secret)), LogFolder);

        Assert.Equal(Format(StartupFailureHandler.BackupFailedWithLogFolderFormat, LogFolder), message);
        Assert.Contains("data is unchanged", message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void BuildMessage_UsesBackupFailedText_WithoutLogFolder(string? logFolder)
    {
        var message = StartupFailureHandler.BuildMessage(new DatabaseBackupFailedException(new IOException(Secret)), logFolder);

        Assert.Equal(StartupFailureHandler.BackupFailedWithoutLogFolder, message);
    }

    [Fact]
    public void BuildMessage_NamesTheBackup_WhenTheMigrationFailedAndTheLogFolderIsKnown()
    {
        var message = StartupFailureHandler.BuildMessage(new DatabaseMigrationFailedException(BackupPath, new InvalidOperationException(Secret)), LogFolder);

        Assert.Equal(Format(StartupFailureHandler.MigrationFailedWithLogFolderFormat, BackupPath, LogFolder), message);
        Assert.Contains(BackupPath, message, StringComparison.Ordinal);
        Assert.Contains(LogFolder, message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildMessage_NamesTheBackup_WhenTheMigrationFailedAndNoLogFolderIsKnown()
    {
        var message = StartupFailureHandler.BuildMessage(new DatabaseMigrationFailedException(BackupPath, new InvalidOperationException(Secret)), null);

        Assert.Equal(Format(StartupFailureHandler.MigrationFailedWithoutLogFolderFormat, BackupPath), message);
        Assert.DoesNotContain(LogFolder, message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildMessage_UsesTheGenericText_ForAnyOtherFailure()
    {
        var withFolder = StartupFailureHandler.BuildMessage(new InvalidOperationException(Secret), LogFolder);
        var withoutFolder = StartupFailureHandler.BuildMessage(new InvalidOperationException(Secret), null);

        Assert.Equal(Format(StartupFailureHandler.MessageWithLogFolderFormat, LogFolder), withFolder);
        Assert.Equal(StartupFailureHandler.MessageWithoutLogFolder, withoutFolder);
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

        Assert.Equal([Format(StartupFailureHandler.MigrationFailedWithLogFolderFormat, BackupPath, LogFolder)], messages);
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

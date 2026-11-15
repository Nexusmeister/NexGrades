using NexGrades.Domain.Maintenance;

namespace NexGrades.Tests;

public class DatabaseBackupFileNameTests
{
    private static readonly DateTime _stamp = new(2026, 11, 15, 9, 5, 3);

    [Fact]
    public void Create_NamesFileFromTimestampAndVersion()
    {
        Assert.Equal("app-20261115-090503-v0.2.1-beta.3.db", DatabaseBackupFileName.Create(_stamp, "0.2.1-beta.3"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1.0/../x")]
    [InlineData("1.0 beta")]
    [InlineData("1.0\evil")]
    [InlineData("vé")]
    public void Create_UsesUnknown_ForMissingOrUnsafeVersion(string? version)
    {
        Assert.Equal("app-20261115-090503-unknown.db", DatabaseBackupFileName.Create(_stamp, version));
    }

    [Fact]
    public void TryParse_ReadsBackTheTimestamp_OfACreatedName()
    {
        var name = DatabaseBackupFileName.Create(_stamp, "1.2.3");

        Assert.True(DatabaseBackupFileName.TryParse(name, out var timestamp));
        Assert.Equal(_stamp, timestamp);
    }

    [Fact]
    public void TryParse_ReadsBackTheTimestamp_OfAnUnknownVersionName()
    {
        var name = DatabaseBackupFileName.Create(_stamp, null);

        Assert.True(DatabaseBackupFileName.TryParse(name, out var timestamp));
        Assert.Equal(_stamp, timestamp);
    }

    [Theory]
    [InlineData("app-20261115-090503-v1.0.0-1.db")]
    [InlineData("app-20261115-090503-unknown-2.db")]
    public void TryParse_AcceptsNames_WithACollisionSuffix(string name)
    {
        Assert.True(DatabaseBackupFileName.TryParse(name, out var timestamp));
        Assert.Equal(_stamp, timestamp);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("app.db")]
    [InlineData("notes.txt")]
    [InlineData("app-foo.db")]
    [InlineData("app-20261115-090503.db")]
    [InlineData("app-20261115-090503-.db")]
    [InlineData("app-20261115-090503-x1.0.db")]
    [InlineData("app-20261115-090503-v.db")]
    [InlineData("app-20261115-090503-v1.0.db.tmp")]
    [InlineData("app-20261115-090503-v1.0.txt")]
    [InlineData("app-20261315-090503-v1.0.db")]
    [InlineData("app-20261115-096503-v1.0.db")]
    [InlineData("app-2026111a-090503-v1.0.db")]
    [InlineData("backup-20261115-090503-v1.0.db")]
    [InlineData("app-20261115-090503-v1 0.db")]
    public void TryParse_Rejects_ForeignNames(string? name)
    {
        Assert.False(DatabaseBackupFileName.TryParse(name, out var timestamp));
        Assert.Equal(default, timestamp);
    }

    [Theory]
    [InlineData("app-20261115-090503-v1.2.0.db", "1.2.0")]
    [InlineData("app-20261115-090503-v1.2.0-1.db", "1.2.0")]
    [InlineData("app-20261115-090503-v1.2.0-12.db", "1.2.0")]
    [InlineData("app-20261115-090503-v1.2.0-beta.1.db", "1.2.0-beta.1")]
    [InlineData("app-20261115-090503-v1.2.0-beta.1-2.db", "1.2.0-beta.1")]
    public void HasVersion_ReturnsTrue_ForNameOfThatVersion(string fileName, string version)
    {
        Assert.True(DatabaseBackupFileName.HasVersion(fileName, version));
    }

    [Fact]
    public void HasVersion_ReturnsFalse_ForPrereleaseNameWhenAskedForReleaseVersion()
    {
        var name = DatabaseBackupFileName.Create(_stamp, "1.2.0-beta.1");

        Assert.False(DatabaseBackupFileName.HasVersion(name, "1.2.0"));
    }

    [Fact]
    public void HasVersion_ReturnsFalse_ForShorterVersionNameWhenAskedForLongerVersion()
    {
        var name = DatabaseBackupFileName.Create(_stamp, "1.2");

        Assert.False(DatabaseBackupFileName.HasVersion(name, "1.2.0"));
    }

    [Fact]
    public void HasVersion_ReturnsFalse_ForLongerVersionNameWhenAskedForShorterVersion()
    {
        var name = DatabaseBackupFileName.Create(_stamp, "1.2.0");

        Assert.False(DatabaseBackupFileName.HasVersion(name, "1.2"));
    }

    [Fact]
    public void HasVersion_ReturnsFalse_ForVersionThatOnlyDiffersByTrailingDigit()
    {
        var name = DatabaseBackupFileName.Create(_stamp, "1.2.01");

        Assert.False(DatabaseBackupFileName.HasVersion(name, "1.2.0"));
    }

    [Theory]
    [InlineData("app-20261115-090503-v1.2.0-.db")]
    [InlineData("app-20261115-090503-v1.2.0-x.db")]
    [InlineData("app-20261115-090503-v1.2.0-1a.db")]
    public void HasVersion_ReturnsFalse_ForMalformedCollisionSuffix(string fileName)
    {
        Assert.False(DatabaseBackupFileName.HasVersion(fileName, "1.2.0"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1.0 beta")]
    [InlineData("1.0/../x")]
    public void HasVersion_ReturnsTrue_ForUnknownNameWhenVersionIsMissingOrUnsafe(string? version)
    {
        var name = DatabaseBackupFileName.Create(_stamp, null);

        Assert.True(DatabaseBackupFileName.HasVersion(name, version));
    }

    [Fact]
    public void HasVersion_ReturnsFalse_ForUnknownNameWhenAskedForARealVersion()
    {
        var name = DatabaseBackupFileName.Create(_stamp, null);

        Assert.False(DatabaseBackupFileName.HasVersion(name, "1.2.0"));
    }

    [Fact]
    public void HasVersion_ReturnsFalse_ForVersionedNameWhenVersionIsMissing()
    {
        var name = DatabaseBackupFileName.Create(_stamp, "1.2.0");

        Assert.False(DatabaseBackupFileName.HasVersion(name, null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("notes.txt")]
    [InlineData("app-20261115-090503-v1.2.0.db.tmp")]
    [InlineData("backup-20261115-090503-v1.2.0.db")]
    public void HasVersion_ReturnsFalse_ForNameThatIsNoBackup(string fileName)
    {
        Assert.False(DatabaseBackupFileName.HasVersion(fileName, "1.2.0"));
    }
}

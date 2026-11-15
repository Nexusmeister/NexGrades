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
}

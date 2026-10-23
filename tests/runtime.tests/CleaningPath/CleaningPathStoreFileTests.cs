using Wjss.Contracts;
using Wjss.Domain;
using Wjss.Runtime.Core.CleaningPath;
using Wjss.Time;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// Phase 3A Owner-local Cleaning Path store file boundary tests: the fixed environment-variable
/// name, the fully-qualified-path requirement, the bounded size, read-once loading through the
/// strict parser, the clock-stamped snapshot, and the never-written rule. All files are synthetic
/// test artifacts in the test temporary directory; no Owner-local path or value appears here.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).
/// </summary>
public sealed class CleaningPathStoreFileTests
{
    [Fact]
    public void Store_Variable_Name_Is_Fixed()
    {
        Assert.Equal("WJSS_CLEANING_PATH_STORE", CleaningPathStoreFile.StoreVariable);
    }

    [Fact]
    public void Unset_Variable_Refuses_Without_A_Fallback()
    {
        var clock = new TestClock();

        var optional = CleaningPathStoreFile.LoadIfConfigured(_ => null, clock);
        Assert.Null(optional);

        var required = CleaningPathStoreFile.Load(_ => null, clock);
        Assert.False(required.Accepted);
        Assert.Null(required.Snapshot);
        Assert.Equal(CleaningPathCodes.StoreNotProvided, Assert.Single(required.Errors).Code);
    }

    [Fact]
    public void Environment_Read_Receives_Exactly_The_Store_Variable()
    {
        string? readName = null;
        CleaningPathStoreFile.LoadIfConfigured(name =>
        {
            readName = name;
            return null;
        }, new TestClock());

        Assert.Equal(CleaningPathStoreFile.StoreVariable, readName);
    }

    [Fact]
    public void Non_Qualified_Location_Is_Refused()
    {
        var result = CleaningPathStoreFile.Load(_ => "relative/store.json", new TestClock());

        Assert.False(result.Accepted);
        Assert.Null(result.Snapshot);
        Assert.Equal(CleaningPathCodes.StoreFileRefused, Assert.Single(result.Errors).Code);
        Assert.All(result.Errors, error => Assert.Null(error.SensorId));
    }

    [Fact]
    public void Missing_File_Is_Refused()
    {
        var location = Path.Combine(Path.GetTempPath(), "wjss-missing-" + Guid.NewGuid().ToString("N") + ".json");

        var result = CleaningPathStoreFile.Load(_ => location, new TestClock());

        Assert.False(result.Accepted);
        Assert.Null(result.Snapshot);
        Assert.Equal(CleaningPathCodes.StoreFileRefused, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void Oversized_File_Is_Refused()
    {
        var location = CreateTempFile(new string('x', (int)CleaningPathStoreFile.MaximumBytes + 1));
        try
        {
            var result = CleaningPathStoreFile.Load(_ => location, new TestClock());

            Assert.False(result.Accepted);
            Assert.Null(result.Snapshot);
            Assert.Equal(CleaningPathCodes.StoreTooLarge, Assert.Single(result.Errors).Code);
        }
        finally
        {
            File.Delete(location);
        }
    }

    [Fact]
    public void Valid_File_Accepts_With_The_Clock_Stamped_Timestamp()
    {
        var start = new DateTimeOffset(2026, 10, 23, 1, 2, 3, TimeSpan.Zero);
        var clock = new TestClock(start);
        var location = CreateTempFile(SyntheticCleaningPathStores.ValidStoreJson());
        try
        {
            var result = CleaningPathStoreFile.Load(_ => location, clock);

            Assert.True(result.Accepted);
            Assert.Empty(result.Errors);
            Assert.NotNull(result.Snapshot);
            Assert.Equal(CleaningPathSourceKind.OwnerLocalFile, result.Snapshot!.SourceKind);
            Assert.Equal(start, result.Snapshot.LoadedAtUtc);
            Assert.Equal(CanonicalSensorMap.SensorLocations, result.Snapshot.Paths.Count);
        }
        finally
        {
            File.Delete(location);
        }
    }

    [Fact]
    public void Store_File_Is_Never_Written_By_Loading()
    {
        var content = SyntheticCleaningPathStores.ValidStoreJson();
        var location = CreateTempFile(content);
        try
        {
            var writtenBefore = File.GetLastWriteTimeUtc(location);
            var lengthBefore = new FileInfo(location).Length;

            CleaningPathStoreFile.Load(_ => location, new TestClock());

            Assert.Equal(content, File.ReadAllText(location));
            Assert.Equal(lengthBefore, new FileInfo(location).Length);
            Assert.Equal(writtenBefore, File.GetLastWriteTimeUtc(location));
        }
        finally
        {
            File.Delete(location);
        }
    }

    [Fact]
    public void Invalid_Content_Refuses_Through_The_Strict_Parser()
    {
        var location = CreateTempFile("{ not json");
        try
        {
            var result = CleaningPathStoreFile.Load(_ => location, new TestClock());

            Assert.False(result.Accepted);
            Assert.Null(result.Snapshot);
            Assert.Equal(CleaningPathCodes.JsonMalformed, Assert.Single(result.Errors).Code);
        }
        finally
        {
            File.Delete(location);
        }
    }

    private static string CreateTempFile(string content)
    {
        var location = Path.Combine(Path.GetTempPath(), "wjss-cleaning-path-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(location, content);
        return location;
    }
}

using Core.Persistence.Settings;
using Npgsql;

namespace Core.Persistence.Tests.Settings;

/// <summary>
/// The shape of a submission, judged in C# before the database (#634, part 2a; decision 0053): a key of a section and a name, each an uppercase letter and then letters, up to 63;
/// a value of 1 to 256 printable ASCII characters; a motive of a lowercase letter and then lowercase letters and underscores, up to 32; and an author with an id. Each refusal is of
/// one form only, so a check that let it pass would be seen. A null or a submission without the shape is a defect of the caller, refused before any connection is opened.
/// </summary>
public sealed class ConfigurationSubmissionTests : IDisposable
{
    private const string Key = "Session:IdleTimeout";
    private const string Value = "00:20:00";
    private const string Motive = "operational_adjustment";

    private readonly NpgsqlDataSource nowhere = NpgsqlDataSource.Create("Host=zqx-nenhum-6341.invalid;Database=zqx;Username=zqx;Timeout=1");

    public static TheoryData<string, string, string, long> Accepted() =>
        new()
        {
            { Key, Value, Motive, 1 },
            { $"{new string('S', 63)}:{new string('N', 63)}", new string('~', 256), new string('m', 32), 1 },
            { "S:N", " ", "m", 1 },
            { Key, Value, "value_correction_", 1 },
        };

    public static TheoryData<string, string, string, long> Refused() =>
        new()
        {
            { Key, Value, Motive, 0 },
            { "session:IdleTimeout", Value, Motive, 1 },
            { "Session:idleTimeout", Value, Motive, 1 },
            { "Se5sion:IdleTimeout", Value, Motive, 1 },
            { "Session:Idle5Timeout", Value, Motive, 1 },
            { $"{new string('S', 64)}:IdleTimeout", Value, Motive, 1 },
            { $"Session:{new string('N', 64)}", Value, Motive, 1 },
            { "Session:IdleTimeout:Extra", Value, Motive, 1 },
            { Key, new string('~', 257), Motive, 1 },
            { Key, "00:20:00é", Motive, 1 },
            { Key, Value, "_operational", 1 },
            { Key, Value, "operationalAdjustment", 1 },
            { Key, Value, "operational-adjustment", 1 },
            { Key, Value, new string('m', 33), 1 },
        };

    [Theory]
    [MemberData(nameof(Accepted))]
    public void ASubmissionInTheShapeOfTheDatabaseHasIt(string key, string value, string motive, long author) =>
        Assert.True(Submission(key, value, motive, author).HasShape);

    [Theory]
    [MemberData(nameof(Refused))]
    public void ASubmissionOutOfTheShapeInOneFormOnlyHasItNot(string key, string value, string motive, long author) =>
        Assert.False(Submission(key, value, motive, author).HasShape);

    [Fact]
    public void ARegisterWithoutADataSourceIsADefectOfTheCaller() =>
        Assert.Equal("dataSource", Assert.Throws<ArgumentNullException>(() => new ConfigurationChanges(null!)).ParamName);

    [Fact]
    public async Task ANullSubmissionIsADefectOfTheCaller()
    {
        var refused = await Assert.ThrowsAsync<ArgumentNullException>(() => new ConfigurationChanges(nowhere).SubmitAsync(null!, TestContext.Current.CancellationToken));

        Assert.Equal("submission", refused.ParamName);
    }

    [Fact]
    public async Task ASubmissionWithoutTheShapeIsRefusedNamingTheShapeBeforeAnyConnection()
    {
        var refused = await Assert.ThrowsAsync<ArgumentException>(
            () => new ConfigurationChanges(nowhere).SubmitAsync(Submission("SessionIdleTimeout", Value, Motive, 1), TestContext.Current.CancellationToken));

        Assert.Equal("submission", refused.ParamName);
        Assert.Contains("Section:Name", refused.Message, StringComparison.Ordinal);
    }

    public void Dispose() => nowhere.Dispose();

    private static ConfigurationSubmission Submission(string key, string value, string motive, long author) =>
        new() { Key = key, Value = value, Motive = motive, AuthorId = author };
}

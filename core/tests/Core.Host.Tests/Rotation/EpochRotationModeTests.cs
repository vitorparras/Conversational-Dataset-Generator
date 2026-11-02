using Core.Host.Rotation;
using Core.Platform.Keys;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Host.Tests.Rotation;

/// <summary>The rotate-epoch mode refuses without touching the database when the custody was not read, and says what happened without a key or an epoch in the words.</summary>
public sealed class EpochRotationModeTests
{
    private const string Unreachable = "Host=127.0.0.1;Port=1;Username=u;Password=zqx-never-printed-7731;Database=d;Timeout=2;Pooling=false";

    [Theory]
    [InlineData("rotate-epoch", true)]
    [InlineData("rotate-epoch --Database:Host=h", true)]
    [InlineData("migrate", false)]
    [InlineData("", false)]
    [InlineData("--MasterKey:File=x rotate-epoch", false)]
    public void ModeIsAskedForByTheFirstArgumentAlone(string line, bool requested)
    {
        ArgumentNullException.ThrowIfNull(line);

        Assert.Equal(requested, EpochRotationMode.Requested(line.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
    }

    [Fact]
    public async Task CustodyNotReadRefusesNamingWhyAndTouchesNothing()
    {
        await using var dataSource = NpgsqlDataSource.Create(Unreachable);
        using var reading = MasterKeyReading.Absent("o arquivo da chave não foi encontrado");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await EpochRotationMode.RotateAsync(dataSource, reading, output, error);

        Assert.Equal((ExitCode.EpochRotationRefused, string.Empty), (exit, output.ToString()));
        Assert.Contains("o arquivo da chave não foi encontrado", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DatabaseThatCannotBeReachedFailsWithItsOwnCodeAndNeverPrintsTheCredential()
    {
        await using var dataSource = NpgsqlDataSource.Create(Unreachable);
        using var reading = MasterKeyReading.FromBytes(
            new byte[MasterKey.Length], DerivationLabels.None, CustodyEpochs.Of(new Dictionary<int, byte[]> { [1] = new byte[MasterKey.Length] }), string.Empty);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await EpochRotationMode.RotateAsync(dataSource, reading, output, error);

        Assert.Equal(ExitCode.EpochRotationFailed, exit);
        Assert.DoesNotContain("zqx-never-printed-7731", error.ToString() + output, StringComparison.Ordinal);
    }

    [Fact]
    public void CustodyFilesTheValuesLackAreNamedInOrder()
    {
        var values = new Dictionary<string, string> { [MasterKeyConfiguration.SecondFilePath] = "x" };

        Assert.Equal(
            [MasterKeyConfiguration.FilePath, MasterKeyConfiguration.ThirdFilePath],
            MasterKeyConfiguration.Missing(values).ToList());
    }

    [Fact]
    public void NoCustodyFileMissingWhenAllThreeAreGiven()
    {
        var values = new Dictionary<string, string>
        {
            [MasterKeyConfiguration.FilePath] = "a",
            [MasterKeyConfiguration.SecondFilePath] = "b",
            [MasterKeyConfiguration.ThirdFilePath] = "c",
        };

        Assert.Empty(MasterKeyConfiguration.Missing(values));
    }
}

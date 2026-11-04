using Core.Host.Loss;
using Core.Persistence.Recovery;
using Core.Platform.Keys;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Host.Tests.Loss;

/// <summary>
/// The record-recovery-test mode (issue 67) is asked for by the first argument alone, refuses a receipt that does not hold before touching the database, refuses when the custody was not read,
/// fails with a code of its own when the database does not answer, and never prints the credential.
/// </summary>
public sealed class RecoveryRecordModeTests
{
    private const string Unreachable = "Host=127.0.0.1;Port=1;Username=u;Password=ZQX-never-printed-6604;Database=d;Timeout=2;Pooling=false";

    [Theory]
    [InlineData("record-recovery-test --receipt x", true)]
    [InlineData("record-recovery-test", true)]
    [InlineData("rehearse-recovery", false)]
    [InlineData("--receipt x record-recovery-test", false)]
    [InlineData("", false)]
    public void ModeIsAskedForByTheFirstArgumentAlone(string line, bool requested)
    {
        ArgumentNullException.ThrowIfNull(line);

        Assert.Equal(requested, RecoveryRecordMode.Requested(line.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
    }

    [Fact]
    public async Task AReceiptThatDoesNotHoldRefusesWithTheReasonBeforeTouchingTheDatabase()
    {
        await using var dataSource = NpgsqlDataSource.Create(Unreachable);
        using var reading = MasterKeyReading.FromBytes(new byte[MasterKey.Length], DerivationLabels.None, "teste");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await RecoveryRecordMode.RecordAsync(dataSource, reading, "zqx", new KeyLossMode.Words(output, error));

        Assert.Equal((ExitCode.RecoveryRefused, string.Empty), (exit, output.ToString()));
        Assert.Contains("recusado, nada foi gravado", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CustodyNotReadRefusesNamingWhyAndTouchesNothing()
    {
        await using var dataSource = NpgsqlDataSource.Create(Unreachable);
        using var reading = MasterKeyReading.Absent("o arquivo da chave não foi encontrado");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await RecoveryRecordMode.RecordAsync(dataSource, reading, "zqx", new KeyLossMode.Words(output, error));

        Assert.Equal((ExitCode.RecoveryRefused, string.Empty), (exit, output.ToString()));
        Assert.Contains("o arquivo da chave não foi encontrado", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DatabaseThatCannotBeReachedFailsWithItsOwnCodeAndNeverPrintsTheCredential()
    {
        await using var dataSource = NpgsqlDataSource.Create(Unreachable);
        using var reading = MasterKeyReading.FromBytes(new byte[MasterKey.Length], DerivationLabels.None, "teste");
        var receipt = new RecoveryReceipt(new DateTimeOffset(2031, 3, 4, 8, 6, 7, TimeSpan.Zero), 1, new string('a', 64), KeyProof.Of(reading.Key!), new SampleCounts(1, 1, 0)).ToText();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await RecoveryRecordMode.RecordAsync(dataSource, reading, receipt, new KeyLossMode.Words(output, error));

        Assert.Equal(ExitCode.RecoveryFailed, exit);
        Assert.DoesNotContain("ZQX-never-printed-6604", error.ToString() + output, StringComparison.Ordinal);
    }
}

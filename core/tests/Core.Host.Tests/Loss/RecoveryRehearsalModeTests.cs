using Core.Host.Loss;
using Core.Platform.Keys;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Host.Tests.Loss;

/// <summary>
/// The rehearse-recovery mode (issue 67) is asked for by the first argument alone, refuses without touching the database when the custody was not read, fails with a code of its own when the
/// database does not answer, and never prints the credential.
/// </summary>
public sealed class RecoveryRehearsalModeTests
{
    private const string Unreachable = "Host=127.0.0.1;Port=1;Username=u;Password=ZQX-never-printed-5512;Database=d;Timeout=2;Pooling=false";

    [Theory]
    [InlineData("rehearse-recovery", true)]
    [InlineData("rehearse-recovery --Database:Host=h", true)]
    [InlineData("declare-key-loss --scope master", false)]
    [InlineData("--Database:Host=h rehearse-recovery", false)]
    [InlineData("", false)]
    public void ModeIsAskedForByTheFirstArgumentAlone(string line, bool requested)
    {
        ArgumentNullException.ThrowIfNull(line);

        Assert.Equal(requested, RecoveryRehearsalMode.Requested(line.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
    }

    [Fact]
    public async Task CustodyNotReadRefusesNamingWhyAndTouchesNothing()
    {
        await using var dataSource = NpgsqlDataSource.Create(Unreachable);
        using var reading = MasterKeyReading.Absent("o arquivo da chave não foi encontrado");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await RecoveryRehearsalMode.RehearseAsync(dataSource, reading, new KeyLossMode.Words(output, error));

        Assert.Equal((ExitCode.RecoveryRefused, string.Empty), (exit, output.ToString()));
        Assert.Contains("o arquivo da chave não foi encontrado", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DatabaseThatCannotBeReachedFailsWithItsOwnCodeAndNeverPrintsTheCredential()
    {
        await using var dataSource = NpgsqlDataSource.Create(Unreachable);
        using var reading = MasterKeyReading.FromBytes(new byte[MasterKey.Length], DerivationLabels.None, "teste");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await RecoveryRehearsalMode.RehearseAsync(dataSource, reading, new KeyLossMode.Words(output, error));

        Assert.Equal(ExitCode.RecoveryFailed, exit);
        Assert.DoesNotContain("ZQX-never-printed-5512", error.ToString() + output, StringComparison.Ordinal);
    }

    [Fact]
    public void TheExitCodesOfTheRecoveryAreDistinctFromEachOtherAndFromTheOtherModes() =>
        Assert.Equal(
            11,
            new[]
            {
                ExitCode.RecoveryRefused, ExitCode.RecoveryFailed, ExitCode.KeyLossRefused, ExitCode.KeyLossFailed, ExitCode.DomainLabelRefused, ExitCode.DomainLabelFailed,
                ExitCode.DomainLabelsMissing, ExitCode.DomainNameTaken, ExitCode.DomainCreationRefused, ExitCode.DomainCreationFailed, ExitCode.EpochRotationRefused,
            }.Distinct().Count());
}

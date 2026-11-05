using Core.Host.Domains;
using Core.Platform.Keys;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Host.Tests.Domains;

/// <summary>
/// The rewrap-domain mode (issue 56, part 2) is asked for by the first argument alone, refuses without touching the database when it cannot go on, fails with a code of its own when the
/// database does not answer, whether it rewraps or only reads the status, and never prints the credential.
/// </summary>
public sealed class DomainRewrapModeTests
{
    private const string Unreachable = "Host=127.0.0.1;Port=1;Username=u;Password=ZQX-never-printed-7731;Database=d;Timeout=2;Pooling=false";

    [Theory]
    [InlineData("rewrap-domain --domain 1", true)]
    [InlineData("rewrap-domain --domain 1 --status", true)]
    [InlineData("rewrap-domain", true)]
    [InlineData("rotate-domain-label --domain 1", false)]
    [InlineData("--domain 1 rewrap-domain", false)]
    [InlineData("", false)]
    public void ModeIsAskedForByTheFirstArgumentAlone(string line, bool requested)
    {
        ArgumentNullException.ThrowIfNull(line);

        Assert.Equal(requested, DomainRewrapMode.Requested(line.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
    }

    [Theory]
    [InlineData("abc", false)]
    [InlineData("0", false)]
    [InlineData("-1", true)]
    [InlineData("1.5", true)]
    [InlineData("99999999999", false)]
    public async Task ADomainThatIsNotAPositiveIntegerRefusesBeforeTouchingTheDatabase(string domain, bool status)
    {
        await using var dataSource = NpgsqlDataSource.Create(Unreachable);
        using var reading = MasterKeyReading.FromBytes(new byte[MasterKey.Length], DerivationLabels.None, "teste");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await DomainRewrapMode.RewrapAsync(dataSource, reading, (domain, status), new DomainGenerationMode.Words(output, error));

        Assert.Equal((ExitCode.DomainRewrapRefused, string.Empty), (exit, output.ToString()));
        Assert.Contains("inteiro positivo", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CustodyNotReadRefusesNamingWhyAndTouchesNothing()
    {
        await using var dataSource = NpgsqlDataSource.Create(Unreachable);
        using var reading = MasterKeyReading.Absent("o arquivo da chave não foi encontrado");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await DomainRewrapMode.RewrapAsync(dataSource, reading, ("1", false), new DomainGenerationMode.Words(output, error));

        Assert.Equal((ExitCode.DomainRewrapRefused, string.Empty), (exit, output.ToString()));
        Assert.Contains("o arquivo da chave não foi encontrado", error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DatabaseThatCannotBeReachedFailsWithItsOwnCodeAndNeverPrintsTheCredential(bool status)
    {
        await using var dataSource = NpgsqlDataSource.Create(Unreachable);
        using var reading = MasterKeyReading.FromBytes(new byte[MasterKey.Length], DerivationLabels.None, "teste");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await DomainRewrapMode.RewrapAsync(dataSource, reading, ("1", status), new DomainGenerationMode.Words(output, error));

        Assert.Equal(ExitCode.DomainRewrapFailed, exit);
        Assert.DoesNotContain("ZQX-never-printed-7731", error.ToString() + output, StringComparison.Ordinal);
    }

    [Fact]
    public void TheExitCodesOfTheRewrapAreThirtyNineAndFortyAndDistinctFromTheOthersOfTheDomain() =>
        Assert.Equal(
            (39, 40, 6),
            (
                (int)ExitCode.DomainRewrapRefused,
                (int)ExitCode.DomainRewrapFailed,
                new[]
                {
                    ExitCode.DomainRewrapRefused, ExitCode.DomainRewrapFailed, ExitCode.DomainLabelRevocationRefused, ExitCode.DomainLabelRevocationFailed,
                    ExitCode.DomainLabelRefused, ExitCode.DomainLabelFailed,
                }.Distinct().Count()));
}

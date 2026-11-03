using Core.Host.Domains;
using Core.Platform.Keys;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Host.Tests.Domains;

/// <summary>
/// The rotate-domain-label and revoke-domain-label modes (issue 56) are asked for by the first argument alone, take their option once, refuse without touching the database when they cannot go
/// on, fail with a code of their own when the database does not answer, and never print the credential.
/// </summary>
public sealed class DomainGenerationModeTests
{
    private const string Unreachable = "Host=127.0.0.1;Port=1;Username=u;Password=ZQX-never-printed-7731;Database=d;Timeout=2;Pooling=false";

    [Theory]
    [InlineData("rotate-domain-label --domain 1", true)]
    [InlineData("revoke-domain-label --label texto-bruto/1/v1", true)]
    [InlineData("rotate-domain-label", true)]
    [InlineData("create-domain --name x", false)]
    [InlineData("--domain 1 rotate-domain-label", false)]
    [InlineData("", false)]
    public void ModeIsAskedForByTheFirstArgumentAlone(string line, bool requested)
    {
        ArgumentNullException.ThrowIfNull(line);

        Assert.Equal(requested, DomainGenerationMode.Requested(line.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
    }

    [Theory]
    [InlineData("rotate-domain-label --domain 7 --Database:Host=h", "7", "--Database:Host=h")]
    [InlineData("rotate-domain-label --Database:Host=h --domain=7", "7", "--Database:Host=h")]
    [InlineData("rotate-domain-label --domain 7 --domain 8", null, "")]
    [InlineData("rotate-domain-label --domain", null, "")]
    [InlineData("rotate-domain-label --domain=", null, "")]
    [InlineData("rotate-domain-label", null, "")]
    public void TheOptionIsTakenOnceAndTheRestGoesToTheConfiguration(string line, string? value, string rest)
    {
        ArgumentNullException.ThrowIfNull(line);

        var (found, remaining) = OptionArguments.Once(line.Split(' ', StringSplitOptions.RemoveEmptyEntries), "--domain");

        Assert.Equal((value, rest), (found, string.Join(' ', remaining)));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("99999999999")]
    public async Task ADomainThatIsNotAPositiveIntegerRefusesBeforeTouchingTheDatabase(string domain)
    {
        await using var dataSource = NpgsqlDataSource.Create(Unreachable);
        using var reading = MasterKeyReading.FromBytes(new byte[MasterKey.Length], DerivationLabels.None, "teste");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await DomainGenerationMode.RotateAsync(dataSource, reading, domain, new DomainGenerationMode.Words(output, error));

        Assert.Equal((ExitCode.DomainLabelRefused, string.Empty), (exit, output.ToString()));
        Assert.Contains("inteiro positivo", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CustodyNotReadRefusesNamingWhyAndTouchesNothing()
    {
        await using var dataSource = NpgsqlDataSource.Create(Unreachable);
        using var reading = MasterKeyReading.Absent("o arquivo da chave não foi encontrado");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await DomainGenerationMode.RotateAsync(dataSource, reading, "1", new DomainGenerationMode.Words(output, error));

        Assert.Equal((ExitCode.DomainLabelRefused, string.Empty), (exit, output.ToString()));
        Assert.Contains("o arquivo da chave não foi encontrado", error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("nome-do-domínio")]
    [InlineData("texto-bruto/1")]
    [InlineData("texto-bruto/01/v1")]
    public async Task ATextThatIsNotALabelRefusesBeforeTouchingTheDatabase(string label)
    {
        await using var dataSource = NpgsqlDataSource.Create(Unreachable);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await DomainGenerationMode.RevokeAsync(dataSource, label, new DomainGenerationMode.Words(output, error));

        Assert.Equal((ExitCode.DomainLabelRefused, string.Empty), (exit, output.ToString()));
        Assert.Contains("não é um rótulo", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DatabaseThatCannotBeReachedFailsWithItsOwnCodeAndNeverPrintsTheCredential()
    {
        await using var dataSource = NpgsqlDataSource.Create(Unreachable);
        using var reading = MasterKeyReading.FromBytes(new byte[MasterKey.Length], DerivationLabels.None, "teste");
        using var output = new StringWriter();
        using var error = new StringWriter();
        var words = new DomainGenerationMode.Words(output, error);

        var rotating = await DomainGenerationMode.RotateAsync(dataSource, reading, "1", words);
        var revoking = await DomainGenerationMode.RevokeAsync(dataSource, "texto-bruto/1/v1", words);

        Assert.Equal((ExitCode.DomainLabelFailed, ExitCode.DomainLabelFailed), (rotating, revoking));
        Assert.DoesNotContain("ZQX-never-printed-7731", error.ToString() + output, StringComparison.Ordinal);
    }

    [Fact]
    public void TheExitCodesOfTheLabelsAreDistinctFromEachOtherAndFromTheDomainAndTheRotationOfTheEpoch() =>
        Assert.Equal(
            7,
            new[]
            {
                ExitCode.DomainLabelRefused, ExitCode.DomainLabelFailed, ExitCode.DomainLabelsMissing, ExitCode.DomainNameTaken,
                ExitCode.DomainCreationRefused, ExitCode.DomainCreationFailed, ExitCode.EpochRotationRefused,
            }.Distinct().Count());
}

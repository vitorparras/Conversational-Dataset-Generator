using Core.Host.Domains;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Host.Tests.Domains;

/// <summary>
/// The revoke-domain-label mode (issue 56) is asked for by the first argument alone, takes its option once, refuses without touching the database when the text is not a label,
/// fails with a code of its own when the database does not answer, and never prints the credential.
/// </summary>
public sealed class DomainRevocationModeTests
{
    private const string Unreachable = "Host=127.0.0.1;Port=1;Username=u;Password=ZQX-never-printed-7731;Database=d;Timeout=2;Pooling=false";

    [Theory]
    [InlineData("revoke-domain-label --label texto-bruto/1/v1", true)]
    [InlineData("revoke-domain-label", true)]
    [InlineData("rotate-domain-label --domain 1", false)]
    [InlineData("--label texto-bruto/1/v1 revoke-domain-label", false)]
    [InlineData("", false)]
    public void ModeIsAskedForByTheFirstArgumentAlone(string line, bool requested)
    {
        ArgumentNullException.ThrowIfNull(line);

        Assert.Equal(requested, DomainRevocationMode.Requested(line.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("nome-do-domínio")]
    [InlineData("texto-bruto/1")]
    [InlineData("texto-bruto/01/v1")]
    public async Task ATextThatIsNotALabelRefusesBeforeTouchingTheDatabase(string target)
    {
        await using var dataSource = NpgsqlDataSource.Create(Unreachable);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await DomainRevocationMode.RevokeAsync(dataSource, target, new DomainGenerationMode.Words(output, error));

        Assert.Equal((ExitCode.DomainLabelRevocationRefused, string.Empty), (exit, output.ToString()));
        Assert.Contains("não é um rótulo", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DatabaseThatCannotBeReachedFailsWithItsOwnCodeAndNeverPrintsTheCredential()
    {
        await using var dataSource = NpgsqlDataSource.Create(Unreachable);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await DomainRevocationMode.RevokeAsync(dataSource, "texto-bruto/1/v1", new DomainGenerationMode.Words(output, error));

        Assert.Equal(ExitCode.DomainLabelRevocationFailed, exit);
        Assert.DoesNotContain("ZQX-never-printed-7731", error.ToString() + output, StringComparison.Ordinal);
    }

    [Fact]
    public void TheExitCodesOfTheRevocationAreThirtySevenAndThirtyEightAndDistinctFromTheRotation() =>
        Assert.Equal(
            (37, 38, 6),
            (
                (int)ExitCode.DomainLabelRevocationRefused,
                (int)ExitCode.DomainLabelRevocationFailed,
                new[]
                {
                    ExitCode.DomainLabelRevocationRefused, ExitCode.DomainLabelRevocationFailed, ExitCode.DomainLabelRefused, ExitCode.DomainLabelFailed,
                    ExitCode.DomainLabelsMissing, ExitCode.RecoveryFailed,
                }.Distinct().Count()));
}

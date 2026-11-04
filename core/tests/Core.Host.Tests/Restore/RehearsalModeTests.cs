using Core.Host.Restore;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Verification;
using Core.Persistence.ServerGuarantees;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Host.Tests.Restore;

/// <summary>
/// The rehearse-restore mode (issue 66, CA-2 and CA-3) is asked for by the first argument alone, refuses a seal that was not given once or that is not read before touching the database,
/// and fails with a code of its own when the database does not answer, without printing the credential.
/// </summary>
public sealed class RehearsalModeTests
{
    private const string Unreachable = "Host=127.0.0.1;Port=1;Username=u;Password=ZQX-never-printed-9921;Database=d;Timeout=2;Pooling=false";

    private static readonly ChainSeal Seal = ChainSeal.Of(new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero), 7, new byte[32]);

    [Theory]
    [InlineData("rehearse-restore --seal x", true)]
    [InlineData("rehearse-restore", true)]
    [InlineData("copy-data --to pasta", false)]
    [InlineData("--seal x rehearse-restore", false)]
    [InlineData("", false)]
    public void ModeIsAskedForByTheFirstArgumentAlone(string line, bool requested)
    {
        ArgumentNullException.ThrowIfNull(line);

        Assert.Equal(requested, RehearsalMode.Requested(line.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
    }

    [Fact]
    public async Task WithoutTheSealTheModeRefusesAndSaysWhy()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var launch = await RehearsalMode.RunAsync(["rehearse-restore"], output, error);

        Assert.Equal((ExitCode.RehearsalRefused, string.Empty), (await launch.RunAsync(), output.ToString()));
        Assert.Contains("informe o selo anotado fora da cópia", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASealThatWasAlteredIsRefusedWithTheWordsOfTheReading()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var altered = Seal.ToText()[..^1] + (Seal.ToText()[^1] == '0' ? "1" : "0");

        var launch = await RehearsalMode.RunAsync(["rehearse-restore", "--seal", altered], output, error);

        Assert.Equal((ExitCode.RehearsalRefused, string.Empty), (await launch.RunAsync(), output.ToString()));
        Assert.Contains(ChainSeal.Read(altered).Refusal!, error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChainThatDoesNotCloseEndsWithTheCodeOfFailureNamesTheReasonAndSaysNothingWasRecorded()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var verification = new ChainVerification
        {
            Scope = VerificationScope.Installation,
            EventsRead = 3,
            VerifiedAt = new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero),
            Recorded = new CustodyResult { Outcome = CustodyOutcome.Appended, Sequence = 4 },
        };
        var result = new RehearsalResult(RehearsalFindings.Chain, verification, new CustodyResult { Outcome = CustodyOutcome.ChainBroken, Refusal = "zqx" });

        var exit = await RehearsalMode.ReportAsync(result, new RehearsalMode.Words(output, error));

        Assert.Equal((ExitCode.RehearsalFailed, string.Empty), (exit, output.ToString()));
        Assert.Contains("falhou (chain)", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("a cadeia não aceitou o evento", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DatabaseThatDoesNotAnswerEndsWithTheCodeOfItsOwnAndNeverPrintsTheCredential()
    {
        await using var dataSource = NpgsqlDataSource.Create(Unreachable);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await RehearsalMode.RehearseAsync(dataSource, Seal, new RehearsalMode.Words(output, error));

        Assert.Equal((ExitCode.RehearsalNotRun, string.Empty), (exit, output.ToString()));
        Assert.DoesNotContain("ZQX-never-printed", error.ToString(), StringComparison.Ordinal);
    }
}

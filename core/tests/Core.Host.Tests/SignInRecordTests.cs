using System.Net;
using Core.Persistence.Custody;
using Core.Persistence.People;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Core.Host.Tests;

/// <summary>A refusal of the entry that does not reach the chain leaves a line in the log with the reason and the prefix of the origin, and never the password.</summary>
public sealed class SignInRecordTests
{
    private const string Password = "ZQX-SENHA-7731";

    [Fact]
    public async Task RefusalThatTheChainTookLeavesNoLine()
    {
        var lines = await RefuseAsync(new CustodyResult { Outcome = CustodyOutcome.Appended, Sequence = 1 });

        Assert.Empty(lines);
    }

    [Fact]
    public async Task RefusalThatTheChainLeftInConflictLeavesALineWithTheReasonAndThePrefix()
    {
        var lines = await RefuseAsync(new CustodyResult { Outcome = CustodyOutcome.Conflict, Refusal = "zqx-recusa" });

        var line = Assert.Single(lines);
        Assert.StartsWith("Warning: ", line, StringComparison.Ordinal);
        Assert.Contains("PasswordWrong", line, StringComparison.Ordinal);
        Assert.Contains("Conflict", line, StringComparison.Ordinal);
        Assert.Contains("prefixo 203.0.113.0/24", line, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefusalFromAnUnknownOriginLeavesALineSayingSo()
    {
        var lines = await RefuseAsync(new CustodyResult { Outcome = CustodyOutcome.Conflict, Refusal = "zqx-recusa" }, unknownOrigin: true);

        Assert.Contains("prefixo desconhecido", Assert.Single(lines), StringComparison.Ordinal);
    }

    private static async Task<IReadOnlyList<string>> RefuseAsync(CustodyResult answer, bool unknownOrigin = false)
    {
        using var recorded = new RecordedLog();
        using var factory = LoggerFactory.Create(logging => logging.AddProvider(recorded));
        await using var source = NpgsqlDataSource.Create("Host=127.0.0.1;Database=zqx_never_opened;Username=zqx;Password=" + Password);
        var record = new SignInRecord(new SignInAttempts(source), (_, _, _, _) => Task.FromResult(answer), factory.CreateLogger<SignInRecord>());

        await record.RefusedAsync(personId: 7, SignInRefusal.PasswordWrong, unknownOrigin ? null : IPAddress.Parse("203.0.113.77"), TestContext.Current.CancellationToken);
        return recorded.Lines;
    }
}

using Core.Persistence.Tests.Fixtures;

namespace Core.Tests.Integration.Sessions;

/// <summary>
/// The identifier of a session is not written to a log: not to the output of the host, nor to its log file,
/// through the entry, the gate that accepts and the gate that refuses, and the exit.
/// </summary>
[Trait("Category", "Database")]
public sealed class SessionIdentifierNotLoggedTests
{
    [DatabaseFact]
    public async Task IdentifierOfASessionIsInNeitherTheOutputNorTheLogFileOfTheHostAfterEntryGateAndExit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await SessionScenario.StartAsync("00:30:00", cancellationToken);
        using var entry = await scenario.EnterAsync(scenario.Name, scenario.Password, cancellationToken);
        var identifier = SessionScenario.IdentifierOf(entry);
        using var accepted = await scenario.ProbeAsync(identifier, cancellationToken);
        using var exit = await scenario.LeaveAsync(identifier, cancellationToken);
        using var refused = await scenario.ProbeAsync(identifier, cancellationToken);

        var written = await scenario.StopAndReadEverythingAsync();

        Assert.Contains("Now listening", written, StringComparison.Ordinal);
        Assert.DoesNotContain(identifier, written, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(scenario.Password, written, StringComparison.Ordinal);
    }
}

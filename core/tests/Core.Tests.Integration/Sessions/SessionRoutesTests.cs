using System.Net;
using System.Text.RegularExpressions;
using Core.Persistence.Tests.Fixtures;

namespace Core.Tests.Integration.Sessions;

/// <summary>
/// The session through the routes of the built host over a real database (issue #70, CA-1 and CA-3, and the evidence for #72, CA-4): the entry hands a cookie with the
/// attributes that keep it on the channel, a closed session is refused when its identifier comes back, a limit passed refuses it with the identifier intact,
/// and no refusal of the entry tells why. The limits themselves are tested over a clock in Core.Platform.Tests.
/// </summary>
[Trait("Category", "Database")]
public sealed partial class SessionRoutesTests
{
    private const string Long = "00:30:00";

    [DatabaseFact]
    public async Task EntryWithTheRightPasswordOpensASessionTheGateAccepts()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await SessionScenario.StartAsync(Long, cancellationToken);
        using var entry = await scenario.EnterAsync(scenario.Name, scenario.Password, cancellationToken);

        using var probe = await scenario.ProbeAsync(SessionScenario.IdentifierOf(entry), cancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, entry.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, probe.StatusCode);
    }

    [DatabaseFact]
    public async Task CookieOfTheEntryIsSecureHttpOnlyStrictForTheWholeHostWithNoDomainAndNoExpiry()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await SessionScenario.StartAsync(Long, cancellationToken);

        using var entry = await scenario.EnterAsync(scenario.Name, scenario.Password, cancellationToken);

        var line = SessionScenario.CookieLineOf(entry);
        Assert.Matches(CookieNamedWith64Hexadecimals(), line);
        Assert.Contains("; secure", line, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("; httponly", line, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("; samesite=strict", line, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("; path=/", line, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", line, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expires=", line, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("max-age=", line, StringComparison.OrdinalIgnoreCase);
    }

    [DatabaseFact]
    public async Task SessionClosedByWhoUsedItIsRefusedWhenTheSameIdentifierComesBack()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await SessionScenario.StartAsync(Long, cancellationToken);
        using var entry = await scenario.EnterAsync(scenario.Name, scenario.Password, cancellationToken);
        var identifier = SessionScenario.IdentifierOf(entry);

        using var exit = await scenario.LeaveAsync(identifier, cancellationToken);
        using var again = await scenario.ProbeAsync(identifier, cancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, exit.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, again.StatusCode);
    }

    [DatabaseFact]
    public async Task ExitOfOneSessionLeavesAnotherOfTheSamePersonOpen()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await SessionScenario.StartAsync(Long, cancellationToken);
        using var first = await scenario.EnterAsync(scenario.Name, scenario.Password, cancellationToken);
        using var second = await scenario.EnterAsync(scenario.Name, scenario.Password, cancellationToken);

        using var exit = await scenario.LeaveAsync(SessionScenario.IdentifierOf(first), cancellationToken);
        using var probe = await scenario.ProbeAsync(SessionScenario.IdentifierOf(second), cancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, probe.StatusCode);
    }

    [DatabaseFact]
    public async Task SessionWithoutActivityForMoreThanTheIdleLimitIsRefusedWithTheIdentifierIntact()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await SessionScenario.StartAsync("00:00:01", cancellationToken);
        using var entry = await scenario.EnterAsync(scenario.Name, scenario.Password, cancellationToken);
        var identifier = SessionScenario.IdentifierOf(entry);

        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        using var late = await scenario.ProbeAsync(identifier, cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, late.StatusCode);
    }

    [DatabaseFact]
    public async Task EntryRefusalIsTheSameForAWrongPasswordAnUnknownNameADeactivatedPersonAndOneWithNoPassword()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await SessionScenario.StartAsync(Long, cancellationToken);

        var refusals = new[]
        {
            await RefusalOfAsync(scenario, scenario.Name, "zqx-senha-errada", cancellationToken),
            await RefusalOfAsync(scenario, "zqx-ninguem", scenario.Password, cancellationToken),
            await RefusalOfAsync(scenario, scenario.NameDeactivated, scenario.Password, cancellationToken),
            await RefusalOfAsync(scenario, scenario.NameWithoutPassword, scenario.Password, cancellationToken),
        };

        Assert.All(refusals, refusal => Assert.Equal(refusals[0], refusal));
        Assert.StartsWith("401 ", refusals[0], StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task EntryRefusedOpensNoSession()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await SessionScenario.StartAsync(Long, cancellationToken);

        using var entry = await scenario.EnterAsync(scenario.Name, "zqx-senha-errada", cancellationToken);

        Assert.False(entry.Headers.Contains("Set-Cookie"));
    }

    private static async Task<string> RefusalOfAsync(SessionScenario scenario, string name, string password, CancellationToken cancellationToken)
    {
        using var response = await scenario.EnterAsync(name, password, cancellationToken);
        return $"{(int)response.StatusCode} {response.Headers.Contains("Set-Cookie")} {await response.Content.ReadAsStringAsync(cancellationToken)}";
    }

    [GeneratedRegex("^__Host-sessao=[0-9A-F]{64};")]
    private static partial Regex CookieNamedWith64Hexadecimals();
}

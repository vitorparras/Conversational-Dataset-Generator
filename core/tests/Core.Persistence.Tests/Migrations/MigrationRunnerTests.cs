using Core.Persistence.Migrations;
using Core.Persistence.Tests.ServerGuarantees;

namespace Core.Persistence.Tests.Migrations;

/// <summary>A server that cannot be reached is a result, not an exception, and neither the result nor its text carries the credential.</summary>
public sealed class MigrationRunnerTests
{
    [Fact]
    public async Task UnreachableServerEndsAsNotConnectedWithNothingApplied()
    {
        await using var server = new UnreachableServer();
        var runner = new MigrationRunner(server.DataSource, EmbeddedMigrations.Load());

        var result = await runner.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(MigrationOutcome.NotConnected, result.Outcome);
        Assert.Empty(result.Applied);
        Assert.DoesNotContain(UnreachableServer.Password, result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(server.ConnectionString, result.ToString(), StringComparison.Ordinal);
    }
}

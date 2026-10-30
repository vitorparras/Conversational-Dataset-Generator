using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Tests.Fixtures;

[assembly: AssemblyFixture(typeof(DatabaseWarmup))]

namespace Core.Persistence.Tests.Fixtures;

/// <summary>
/// Starts the database before the first test when the mutation step asked to reuse it, so that the time a test takes is the
/// test's own and not the wait for Postgres, which is what Stryker turns into the timeout of every mutant. Without the request
/// nothing starts early: a run that selects no database test never starts one.
/// </summary>
[SuppressMessage("Performance", "CA1812", Justification = "xunit creates the assembly fixture.")]
internal sealed class DatabaseWarmup : IAsyncLifetime
{
    public async ValueTask InitializeAsync()
    {
        if (DatabaseReuse.Token is not null && DockerAvailability.ShouldRun)
        {
            _ = await PostgresFixture.SharedAsync();
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

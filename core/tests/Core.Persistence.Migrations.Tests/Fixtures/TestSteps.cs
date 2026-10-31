using System.Text;
using Core.Persistence.Migrations;

namespace Core.Persistence.Tests.Fixtures;

/// <summary>Steps a test injects into the runner: the ones this build carries, followed by steps of the test's own.</summary>
internal static class TestSteps
{
    /// <summary>The given steps, then one step per entry, numbered after the last one and named <c>NNNN-name</c>.</summary>
    public static IReadOnlyList<MigrationStep> Following(IEnumerable<MigrationStep> before, params (string Name, string Sql)[] added)
    {
        var steps = new List<MigrationStep>(before);
        foreach (var (name, sql) in added)
        {
            var version = steps[^1].Version + 1;
            steps.Add(MigrationStep.Of(version, $"{version:0000}-{name}", Encoding.UTF8.GetBytes(sql)));
        }

        return steps;
    }

    /// <summary>The steps this build carries, then one step per entry.</summary>
    public static IReadOnlyList<MigrationStep> OnTopOfTheEmbedded(params (string Name, string Sql)[] added) =>
        Following(EmbeddedMigrations.Load(), added);
}

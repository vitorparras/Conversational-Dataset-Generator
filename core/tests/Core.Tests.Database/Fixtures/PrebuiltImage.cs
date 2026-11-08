namespace Core.Persistence.Tests.Fixtures;

/// <summary>
/// The database image the verification built once, before the tests, from tools/database, and handed over by name through
/// <see cref="Variable"/>. Every test project that needs a server starts its container from it, so the run builds the image once and a
/// build that fails fails the step that builds it, with its own log, and not each test project with a bare "has not been created".
/// Without the variable every test run builds its own image from tools/database.
/// </summary>
internal static class PrebuiltImage
{
    // No CDG_ prefix: the hosts the tests start read every CDG_ variable as configuration and refuse the one they do not know (exit 78).
    public const string Variable = "TEST_DATABASE_IMAGE";

    /// <summary>The name and tag of the image that already exists in the Docker daemon, or null when the run has to build it.</summary>
    public static string? Name
    {
        get
        {
            var name = Environment.GetEnvironmentVariable(Variable);
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
    }
}

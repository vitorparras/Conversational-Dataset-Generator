using System.Security.Cryptography;
using System.Text;

namespace Core.Persistence.Tests.Fixtures;

/// <summary>
/// What the mutation step asks of the test run through <see cref="Variable"/>: one database image and one container, named after
/// a token the step made up, that outlive the test server. Stryker kills and restarts the server after a mutant times out, and
/// without this every restart builds the image and starts Postgres again. The step removes both when it ends. Without the
/// variable every run builds, starts and discards its own Postgres.
/// </summary>
internal static class DatabaseReuse
{
    public const string Variable = "CDG_TEST_DATABASE_REUSE";

    /// <summary>The token the step handed over, or null when the run is not asked to reuse.</summary>
    public static string? Token
    {
        get
        {
            var token = Environment.GetEnvironmentVariable(Variable);
            return string.IsNullOrWhiteSpace(token) ? null : token;
        }
    }

    public static string ContainerName(string token) => $"cdg-test-database-{token}";

    public static string ImageName(string token) => $"cdg-test-database:{token}";

    /// <summary>The password of a role, the same in every server of the run, since the container outlives them; it is never reused outside the run.</summary>
    public static string Password(string token, string role) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(role)));
}

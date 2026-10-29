using System.Security.Cryptography;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Npgsql;

namespace Core.Persistence.Tests.Fixtures;

/// <summary>
/// The image built from tools/database, started once for the whole test run on first use, with the connection
/// strings of the roles app and owner. Testcontainers' reaper removes the container and the image when the run ends.
/// </summary>
internal sealed class PostgresFixture
{
    private const int ServerPort = 5432;
    private const string DatabaseName = "cdg";
    private static readonly TimeSpan HealthyWithin = TimeSpan.FromMinutes(3);
    private static readonly Lazy<Task<PostgresFixture>> Shared = new(StartAsync);

    private PostgresFixture(string appConnectionString, string ownerConnectionString, string superuserConnectionString)
    {
        AppConnectionString = appConnectionString;
        OwnerConnectionString = ownerConnectionString;
        SuperuserConnectionString = superuserConnectionString;
    }

    /// <summary>The application role: connects, owns nothing.</summary>
    public string AppConnectionString { get; }

    /// <summary>The role that owns the database and its schema.</summary>
    public string OwnerConnectionString { get; }

    /// <summary>The superuser, for a test to arrange what neither app nor owner may do; never the role a check runs as by default.</summary>
    public string SuperuserConnectionString { get; }

    /// <summary>The running database every test of the run shares; the first caller builds the image and starts it.</summary>
    public static Task<PostgresFixture> SharedAsync() => Shared.Value;

    private static async Task<PostgresFixture> StartAsync()
    {
        var image = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(DatabaseTool.Directory)
            .WithDockerfile("Dockerfile")
            .Build();
        await image.CreateAsync();
        var superuserPassword = NewPassword();
        var ownerPassword = NewPassword();
        var appPassword = NewPassword();
        var container = new ContainerBuilder(image)
            .WithEnvironment("POSTGRES_PASSWORD", superuserPassword)
            .WithEnvironment("DATABASE_OWNER_PASSWORD", ownerPassword)
            .WithEnvironment("DATABASE_APP_PASSWORD", appPassword)
            .WithPortBinding(ServerPort, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilContainerIsHealthy(waitStrategyModifier: wait => wait.WithTimeout(HealthyWithin)))
            .Build();
        await container.StartAsync();
        return new PostgresFixture(
            ConnectionString(container, "app", appPassword),
            ConnectionString(container, "owner", ownerPassword),
            ConnectionString(container, "postgres", superuserPassword));
    }

    private static string NewPassword() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

    private static string ConnectionString(IContainer container, string role, string password) =>
        new NpgsqlConnectionStringBuilder
        {
            Host = container.Hostname,
            Port = container.GetMappedPublicPort(ServerPort),
            Database = DatabaseName,
            Username = role,
            Password = password,
        }.ConnectionString;
}

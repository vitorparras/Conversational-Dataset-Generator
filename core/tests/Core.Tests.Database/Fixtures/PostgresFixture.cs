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

    private readonly IContainer container;

    private PostgresFixture(IContainer container, string appConnectionString, string ownerConnectionString, string superuserConnectionString)
    {
        this.container = container;
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
        var reuse = DatabaseReuse.Token;
        var imageBuilder = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(DatabaseTool.Directory)
            .WithDockerfile("Dockerfile");
        if (reuse is not null)
        {
            imageBuilder = imageBuilder.WithName(DatabaseReuse.ImageName(reuse)).WithDeleteIfExists(false).WithImageBuildPolicy(found => string.IsNullOrEmpty(found?.ID));
        }

        var image = imageBuilder.Build();
        await image.CreateAsync();
        var superuserPassword = reuse is null ? NewPassword() : DatabaseReuse.Password(reuse, "postgres");
        var ownerPassword = reuse is null ? NewPassword() : DatabaseReuse.Password(reuse, "owner");
        var appPassword = reuse is null ? NewPassword() : DatabaseReuse.Password(reuse, "app");
        var builder = new ContainerBuilder(image)
            .WithEnvironment("POSTGRES_PASSWORD", superuserPassword)
            .WithEnvironment("DATABASE_OWNER_PASSWORD", ownerPassword)
            .WithEnvironment("DATABASE_APP_PASSWORD", appPassword)
            .WithPortBinding(ServerPort, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilContainerIsHealthy(waitStrategyModifier: wait => wait.WithTimeout(HealthyWithin)));
        if (reuse is not null)
        {
            builder = builder.WithName(DatabaseReuse.ContainerName(reuse)).WithReuse(_ => reuse);
        }

        var container = builder.Build();
        await container.StartAsync();
        var fixture = new PostgresFixture(
            container,
            ConnectionString(container, "app", appPassword),
            ConnectionString(container, "owner", ownerPassword),
            ConnectionString(container, "postgres", superuserPassword));
        if (reuse is not null)
        {
            await fixture.DropScratchDatabasesAsync();
        }

        return fixture;
    }

    /// <summary>What pg_dump, run inside the server as the superuser, writes for the database: schema, data and comments.</summary>
    public async Task<string> DumpAsync(string database)
    {
        var dump = await container.ExecAsync(["pg_dump", "--username=postgres", $"--dbname={database}", "--inserts"], TestContext.Current.CancellationToken);
        return dump.ExitCode == 0 ? dump.Stdout : throw new InvalidOperationException($"pg_dump exited with {dump.ExitCode}: {dump.Stderr}");
    }

    /// <summary>
    /// Everything the server wrote to its log directory so far, read inside the server: the csvlog files and the ones the collector keeps for
    /// what reaches the error stream. The directory is outside the data directory, as the image sets it.
    /// </summary>
    public async Task<string> ServerLogAsync()
    {
        var log = await container.ExecAsync(["sh", "-c", "cat /var/log/postgresql/*"], TestContext.Current.CancellationToken);
        return log.ExitCode == 0 ? log.Stdout : throw new InvalidOperationException($"reading the server log exited with {log.ExitCode}: {log.Stderr}");
    }

    /// <summary>
    /// A server that outlives its test run keeps the scratch databases of the tests a restart cut short; each test makes its own,
    /// so none of them is needed and none is left to the next run.
    /// </summary>
    private async Task DropScratchDatabasesAsync()
    {
        await using var connection = new NpgsqlConnection(SuperuserConnectionString);
        await connection.OpenAsync();
        var names = new List<string>();
        await using (var list = new NpgsqlCommand("SELECT datname FROM pg_database WHERE datname LIKE 'zqx!_mig!_%' ESCAPE '!'", connection))
        await using (var reader = await list.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                names.Add(reader.GetString(0));
            }
        }

        foreach (var name in names)
        {
            await using var drop = new NpgsqlBatch(connection);
            drop.BatchCommands.Add(new NpgsqlBatchCommand($"DROP DATABASE IF EXISTS {name} WITH (FORCE)"));
            _ = await drop.ExecuteNonQueryAsync();
        }
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

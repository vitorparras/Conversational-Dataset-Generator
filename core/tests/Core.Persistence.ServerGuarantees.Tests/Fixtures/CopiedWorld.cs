using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Core.Persistence.ServerGuarantees;
using Core.Platform.Configuration;
using Npgsql;

namespace Core.Persistence.Tests.Fixtures;

/// <summary>A migrated database with a short chain and content sealed under a synthetic key, and the copy of its data made by the same code the operator runs, in a folder of its own.</summary>
internal sealed class CopiedWorld : IAsyncDisposable
{
    public const string Marker = "texto bruto sintetico zqx";

    private CopiedWorld(FidelityWorld world, string folder, DataCopyResult result)
    {
        World = world;
        Folder = folder;
        Result = result;
    }

    public FidelityWorld World { get; }

    public string Folder { get; }

    public DataCopyResult Result { get; }

    /// <summary>The configuration given to the copy, the way the host shows it: what is secret already redacted.</summary>
    public static IReadOnlyList<EffectiveEntry> Configuration { get; } = [
        new("Database:Host", "127.0.0.1:5432", ConfigurationOrigin.File),
        new("Database:Password", EffectiveConfiguration.Redacted, ConfigurationOrigin.Environment),
        new("MasterKey:File", EffectiveConfiguration.Redacted, ConfigurationOrigin.Argument),
    ];

    /// <param name="betweenTables">Runs after each table is written, with the world and the name of the table, so a test can write while the copy is under way.</param>
    /// <param name="beforeCopy">Statements the owner runs after the world is made and before the copy is, such as the registering and the revocation of a label.</param>
    /// <param name="openBatch">Whether a batch is left open, with two items and a map, in the first domain when the copy is made: the batch is the third, after the two seeded.</param>
    [SuppressMessage("Reliability", "CA2000", Justification = "The world goes to the CopiedWorld that is returned, which disposes it.")]
    public static async Task<CopiedWorld> CreateAsync(Func<FidelityWorld, string, Task>? betweenTables = null, string[]? beforeCopy = null,
        bool openBatch = false)
    {
        var world = await FidelityWorld.CreateAsync();
        await world.RunAsOwnerAsync(
            "INSERT INTO public.custody_event (actor_person_id, item_kind, item_id, step, reason) VALUES (1, 'person', 1, 'person_created', 'administrative_act')",
            "INSERT INTO public.custody_event (actor_person_id, item_kind, item_id, step, reason) VALUES (1, 'person', 2, 'person_created', 'administrative_act')",
            "INSERT INTO public.sign_in_attempt (network) VALUES ('203.0.113.0/24')");
        if (beforeCopy is not null)
        {
            await world.RunAsOwnerAsync(beforeCopy);
        }

        var item = await world.Writer.AddItemTextAsync(1, ItemTexts.Of(Marker, "texto sanitizado sintetico zqx"), TestContext.Current.CancellationToken);
        _ = await world.Writer.AddReversalMapAsync(1, item, "mapa sintetico zqx", TestContext.Current.CancellationToken);
        if (openBatch)
        {
            await world.LeaveABatchOpenAsync();
        }

        var folder = Path.Combine(Path.GetTempPath(), "zqx-copy-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        await using var owner = NpgsqlDataSource.Create(world.OwnerConnectionString);
        var result = await new DataCopy(owner, TimeProvider.System) { AfterTable = table => betweenTables is null ? Task.CompletedTask : betweenTables(world, table) }
            .WriteAsync(folder, Configuration, TestContext.Current.CancellationToken);
        return new CopiedWorld(world, folder, result);
    }

    public async ValueTask DisposeAsync()
    {
        await World.DisposeAsync();
        if (Directory.Exists(Folder))
        {
            Directory.Delete(Folder, recursive: true);
        }
    }
}

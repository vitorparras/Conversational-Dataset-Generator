using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Migrations;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Tests.Fixtures;

/// <summary>A copy of the data put back into a database of its own by the functions of the load, in the order of the 005, with the window closed approved, as the load of a restore leaves it.</summary>
internal sealed class RestoredCopy : IAsyncDisposable
{
    private static readonly string[] Tables =
    [
        "public.domain", "public.derivation_label", "public.person", "public.person_credential", "public.person_change", "public.custody_event",
        "content.item_text", "content.reversal_map", "content.item_emptied",
    ];

    private RestoredCopy(ScratchDatabase database, NpgsqlDataSource app)
    {
        Database = database;
        App = app;
    }

    public ScratchDatabase Database { get; }

    /// <summary>The data source of the application's role over the restored database.</summary>
    public NpgsqlDataSource App { get; }

    public static async Task<RestoredCopy> LoadAsync(string folder)
    {
        var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        var restored = new RestoredCopy(database, NpgsqlDataSource.Create(database.AppConnectionString));
        _ = await restored.CallAsync("restore_open");
        foreach (var table in Tables)
        {
            var lines = await File.ReadAllLinesAsync(Path.Combine(folder, table + ".jsonl"), TestContext.Current.CancellationToken);
            var rows = "[" + string.Join(',', lines.Where(line => line.Length > 0)) + "]";
            _ = table == "public.custody_event" ? await restored.CallAsync("restore_events", rows) : await restored.CallAsync("restore_load", table, rows);
        }

        _ = await restored.CallAsync("restore_close", true);
        return restored;
    }

    public async ValueTask DisposeAsync()
    {
        await App.DisposeAsync();
        await Database.DisposeAsync();
    }

    [SuppressMessage("Security", "CA2100", Justification = "The function is a literal of the calling tests and the arguments are parameters.")]
    private async Task<object?> CallAsync(string function, params object[] arguments)
    {
        await using var command = App.CreateCommand($"SELECT public.{function}({string.Join(", ", arguments.Select((_, index) => $"@p{index}"))})");
        for (var index = 0; index < arguments.Length; index++)
        {
            var parameter = new NpgsqlParameter($"p{index}", arguments[index]);
            parameter.NpgsqlDbType = arguments[index] is string text && text.StartsWith('[') ? NpgsqlDbType.Jsonb : parameter.NpgsqlDbType;
            _ = command.Parameters.Add(parameter);
        }

        var result = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        return result is DBNull ? null : result;
    }
}

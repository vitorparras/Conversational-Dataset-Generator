namespace Core.Persistence.Tests.Content;

/// <summary>
/// The tampering of criterion 4 of requirement 003, shared by the two tests that run it: the texts and the keys. Each tampering takes a dump and a reading of every file of the server,
/// so the two groups are tests of classes of their own, which run beside the other tests of the storage and not after them; in one class, the group of the texts made the class the
/// longest wait of each mutant the seeding reaches, past the time the mutation testing gives it. One test per column would seed the storage once per column for each of those mutants.
/// </summary>
internal static class OpaqueTampering
{
    /// <summary>Writes the columns of the group in clear one after another, and after each the detector names exactly the ones written so far, the keys found in the files too.</summary>
    public static async Task TamperOneAfterAnotherAsync(IReadOnlyList<string> group, Func<OpaqueStorage.Material, IEnumerable<string>> seeded)
    {
        await using var world = await ContentWorld.CreateAsync();
        var material = await OpaqueStorage.SeedAsync(world);
        Assert.Equal(group.Order(StringComparer.Ordinal), seeded(material).Order(StringComparer.Ordinal));
        var tampered = new List<string>();

        foreach (var column in group.Order(StringComparer.Ordinal))
        {
            await WriteInClearAsync(world, column, material.InClear(column));
            tampered.Add(column);
            var observation = await OpaqueStorage.ObserveAsync(world, material);
            Assert.Equal(tampered, OpaqueStorage.ColumnsShown(observation, material, [.. material.Columns]));
            // The files of the server hold the key written in clear, raw as a bytea is stored: a search that never matched raw bytes would let a key through while the dump alone caught it.
            Assert.Equal(tampered.Where(material.Keys.ContainsKey).Order(StringComparer.Ordinal), observation.KeysInFiles.Order(StringComparer.Ordinal));
        }
    }

    /// <summary>The defect the detector exists for: the column holds the text as it is, bytes of the text and no envelope, put there by whoever the path of the application does not bind.</summary>
    private static async Task WriteInClearAsync(ContentWorld world, string column, byte[] clear)
    {
        var parts = column.Split('.');
        // The content changes only by the emptying or the rerun (CE004), a key is wrapped again only with the event of the rotation (CE005) and a pseudonymized text comes only with its
        // map (CE005): triggers that fire for the owner too. Whoever writes in clear sets them aside for the one statement, as an owner would.
        var table = $"{parts[0]}.{parts[1]}";
        string[] gates = parts[1] switch
        {
            "item_text" => ["emptied_content_is_final", "item_change_needs_event", "resanitization_needs_rerun", "pseudonymized_needs_map"],
            "reversal_map" => ["emptied_content_is_final", "map_change_needs_event"],
            "date_shift" => ["date_shift_change_is_bounded"],
            _ => ["origin_change_is_bounded"],
        };
        // nosemgrep: sql-concatenation-csharp interpolates the names of a column read from the catalog of a throwaway database, never input.
        var statement = string.Concat(gates.Select(gate => $"ALTER TABLE {table} DISABLE TRIGGER {gate}; "))
            + $"UPDATE {table} SET {parts[2]} = @text; " + string.Concat(gates.Select(gate => $"ALTER TABLE {table} ENABLE ALWAYS TRIGGER {gate}; "));
        await world.Database.ExecuteAsSuperuserAsync(statement, ("text", clear));
    }
}

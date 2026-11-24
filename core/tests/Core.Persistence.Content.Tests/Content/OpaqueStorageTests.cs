using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Content;

/// <summary>
/// The proof of requirement 003: a copy of the storage, read without the key, shows nothing of what was submitted. It reads by the side of the application, in two ways,
/// the dump the server writes and the files of the server read as bytes after a CHECKPOINT, and it finds the columns in the catalog, so that a protected column added
/// tomorrow is read without anyone adding it here.
/// </summary>
[Trait("Category", "Database")]
public sealed class OpaqueStorageTests
{
    [DatabaseFact]
    public async Task Ca1TheRawTextAndTheEntriesOfTheMapAreInNeitherTheDumpNorTheFilesOfTheServer()
    {
        await using var world = await ContentWorld.CreateAsync();
        var material = await OpaqueStorage.SeedAsync(world);

        var observation = await OpaqueStorage.ObserveAsync(world, material);

        // Every marked column but the sanitized text, which is CA-2: the two named by the criterion and any a later migration marks.
        Assert.Empty(OpaqueStorage.ColumnsShown(observation, material, [.. material.Columns.Where(column => column != OpaqueStorage.SanitizedText)]));
    }

    [DatabaseFact]
    public async Task Ca2TheSanitizedTextIsInNeitherTheDumpNorTheFilesOfTheServer()
    {
        await using var world = await ContentWorld.CreateAsync();
        var material = await OpaqueStorage.SeedAsync(world);

        var observation = await OpaqueStorage.ObserveAsync(world, material);

        Assert.Empty(OpaqueStorage.ColumnsShown(observation, material, OpaqueStorage.SanitizedText));
    }

    [DatabaseFact]
    public async Task Ca3TheTextKeptInClearOnPurposeIsFoundInTheDumpAndInTheFilesSoThatNotFindingTheOthersIsNotNotWriting()
    {
        await using var world = await ContentWorld.CreateAsync();
        var material = await OpaqueStorage.SeedAsync(world);

        var observation = await OpaqueStorage.ObserveAsync(world, material);

        Assert.Equal([true, true], [observation.ShowsInDump(material.Pseudonymized), observation.ShowsInFiles(material.Pseudonymized)]);
    }

    // The columns in two groups, the texts and the keys, and not all in one test: each tampering takes a dump and a reading of every file of the server, and the whole list in one
    // test outgrew the time the mutation testing gives a single test; one test per column would seed the storage once per column for every mutant the seeding reaches.
    [DatabaseFact]
    public Task Ca4TextColumnsThatWriteInClearFailTheDetectorAndAreNamedTogether() =>
        TamperOneAfterAnotherAsync(OpaqueStorage.TextColumns, material => material.Texts.Keys);

    [DatabaseFact]
    public Task Ca4KeyColumnsThatWriteInClearFailTheDetectorAndAreNamedTogether() =>
        TamperOneAfterAnotherAsync(OpaqueStorage.KeyColumnsWrapped, material => material.Keys.Keys);

    [DatabaseFact]
    public async Task Ca5TheKeyInNoWayItIsWrittenIsInTheDumpOrInAnyFileOfTheServerWhileTheSearchStillFindsWhatIsThere()
    {
        await using var world = await ContentWorld.CreateAsync();
        var material = await OpaqueStorage.SeedAsync(world);

        var observation = await OpaqueStorage.ObserveAsync(world, material, [.. OpaqueStorage.KeyAsNeedles()]);

        Assert.Equal(
            (false, false, true),
            (observation.DumpShowsAny(OpaqueStorage.KeyAsText()), observation.FilesBeyond(material).Any(), observation.ShowsInFiles(material.Pseudonymized)));
    }

    /// <summary>The defect the detector exists for: the column holds the text as it is, bytes of the text and no envelope, put there by whoever the path of the application does not bind.</summary>
    /// <summary>Writes the columns of the group in clear one after another, and after each the detector names exactly the ones written so far, the keys found in the files too.</summary>
    private static async Task TamperOneAfterAnotherAsync(IReadOnlyList<string> group, Func<OpaqueStorage.Material, IEnumerable<string>> seeded)
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

    private static async Task WriteInClearAsync(ContentWorld world, string column, byte[] clear)
    {
        var parts = column.Split('.');
        // The content changes only by the emptying or the rerun (CE004) and a key is wrapped again only with the event of the rotation (CE005): triggers that fire for the owner too.
        // Whoever writes in clear sets them aside for the one statement, as an owner would.
        var table = $"{parts[0]}.{parts[1]}";
        string[] gates = parts[1] switch
        {
            "item_text" => ["emptied_content_is_final", "item_change_needs_event", "resanitization_needs_rerun"],
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

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

    [DatabaseFact]
    public async Task Ca4AProtectedColumnThatWritesInClearFailsTheDetectorAndIsNamed()
    {
        await using var world = await ContentWorld.CreateAsync();
        var material = await OpaqueStorage.SeedAsync(world);
        var tampered = new List<string>();

        foreach (var column in material.Columns.Order(StringComparer.Ordinal))
        {
            await WriteInClearAsync(world, column, material.InClear(column));
            tampered.Add(column);
            var observation = await OpaqueStorage.ObserveAsync(world, material);
            Assert.Equal(tampered, OpaqueStorage.ColumnsShown(observation, material, [.. material.Columns]));
        }
    }

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
    private static async Task WriteInClearAsync(ContentWorld world, string column, byte[] clear)
    {
        var parts = column.Split('.');
        // The content changes only by the emptying (CE004) and a key is wrapped again only with the event of the rotation (CE005): triggers that fire for the owner too.
        // Whoever writes in clear sets them aside for the one statement, as an owner would.
        var table = $"{parts[0]}.{parts[1]}";
        var gate = parts[1] == "item_text" ? "item_change_needs_event" : "map_change_needs_event";
        // nosemgrep: sql-concatenation-csharp interpolates the names of a column read from the catalog of a throwaway database, never input.
        var statement = $"ALTER TABLE {table} DISABLE TRIGGER emptied_content_is_final; ALTER TABLE {table} DISABLE TRIGGER {gate}; UPDATE {table} SET {parts[2]} = @text; "
            + $"ALTER TABLE {table} ENABLE ALWAYS TRIGGER emptied_content_is_final; ALTER TABLE {table} ENABLE ALWAYS TRIGGER {gate}";
        await world.Database.ExecuteAsSuperuserAsync(statement, ("text", clear));
    }
}

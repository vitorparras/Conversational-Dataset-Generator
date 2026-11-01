using System.Text;
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
        Assert.Empty(OpaqueStorage.ColumnsShown(observation, material, [.. material.Texts.Keys.Where(column => column != OpaqueStorage.SanitizedText)]));
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

        foreach (var column in material.Texts.Keys.Order(StringComparer.Ordinal))
        {
            await WriteInClearAsync(world, column, material.Texts[column]);
            tampered.Add(column);
            var observation = await OpaqueStorage.ObserveAsync(world, material);
            Assert.Equal(tampered, OpaqueStorage.ColumnsShown(observation, material, [.. material.Texts.Keys]));
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
    private static async Task WriteInClearAsync(ContentWorld world, string column, string text)
    {
        var parts = column.Split('.');
        // nosemgrep: sql-concatenation-csharp interpolates the names of a column read from the catalog of a throwaway database, never input.
        var statement = $"UPDATE {parts[0]}.{parts[1]} SET {parts[2]} = @text";
        await world.Database.ExecuteAsSuperuserAsync(statement, ("text", Encoding.UTF8.GetBytes(text)));
    }
}

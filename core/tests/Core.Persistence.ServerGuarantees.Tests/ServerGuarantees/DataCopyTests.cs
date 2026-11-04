using System.Globalization;
using System.Text.Json.Nodes;
using Core.Persistence.Content.Writing;
using Core.Persistence.ServerGuarantees;
using Core.Persistence.Tests.Content;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>
/// The copy of the data (issue 66, CA-1): a folder with the manifest, the configuration and one file for each table, and nothing else; without the key, the files of the custody or any secret, by
/// the way it is made and not by a list of what to leave out; and the seal of the top of the chain recorded in the chain and given back apart from the folder.
/// </summary>
[Trait("Category", "Database")]
public sealed class DataCopyTests
{
    private static readonly string[] Files =
    [
        "configuration.json", "content.item_emptied.jsonl", "content.item_text.jsonl", "content.reversal_map.jsonl", "manifest.json", "public.custody_event.jsonl",
        "public.derivation_label.jsonl", "public.domain.jsonl", "public.person.jsonl", "public.person_change.jsonl", "public.person_credential.jsonl",
        "public.sign_in_attempt.jsonl",
    ];

    [DatabaseFact]
    public async Task Ca1FolderHoldsTheManifestTheConfigurationAndOneFileForEachTableAndNothingElse()
    {
        await using var copied = await CopiedWorld.CreateAsync();

        var manifest = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(copied.Folder, "manifest.json"), TestContext.Current.CancellationToken))!;
        var version = Assert.Single(await copied.World.OwnerColumnAsync("SELECT max(version)::text FROM public.schema_version"));

        Assert.Equal(DataCopyOutcome.Made, copied.Result.Outcome);
        Assert.Equal(Files, Directory.GetFiles(copied.Folder).Select(file => Path.GetFileName(file)).Order(StringComparer.Ordinal));
        Assert.Equal((1, int.Parse(version, CultureInfo.InvariantCulture), 10), ((int)manifest["format"]!, (int)manifest["schemaVersion"]!, manifest["files"]!.AsArray().Count));
    }

    [DatabaseFact]
    public async Task Ca1TheManifestListsTheTablesInTheOrderOfTheLoadWithTheAttemptsToSignInAfterThePeople()
    {
        await using var copied = await CopiedWorld.CreateAsync();

        var manifest = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(copied.Folder, "manifest.json"), TestContext.Current.CancellationToken))!;
        var attempts = await File.ReadAllLinesAsync(Path.Combine(copied.Folder, "public.sign_in_attempt.jsonl"), TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                "public.domain.jsonl", "public.derivation_label.jsonl", "public.person.jsonl", "public.person_credential.jsonl", "public.person_change.jsonl",
                "public.sign_in_attempt.jsonl", "public.custody_event.jsonl", "content.item_text.jsonl", "content.reversal_map.jsonl", "content.item_emptied.jsonl",
            ],
            manifest["files"]!.AsArray().Select(file => (string)file!));
        Assert.Equal("203.0.113.0/24", (string)JsonNode.Parse(Assert.Single(attempts))!["network"]!);
    }

    [DatabaseFact]
    public async Task Ca1TheRowsOfTheChainComeBackWithTheSequenceAndTheDigestsTheDatabaseHas()
    {
        await using var copied = await CopiedWorld.CreateAsync();

        var lines = await File.ReadAllLinesAsync(Path.Combine(copied.Folder, "public.custody_event.jsonl"), TestContext.Current.CancellationToken);
        var tip = JsonNode.Parse(lines[^1])!;
        var stored = Assert.Single(await copied.World.OwnerColumnAsync("SELECT sequence || ':' || encode(digest, 'hex') FROM public.custody_event ORDER BY sequence DESC LIMIT 1"));

        Assert.Equal(3, lines.Length);
        Assert.Equal(stored, $"{(long)tip["sequence"]!}:{((string)tip["digest"]!)[2..]}");
    }

    [DatabaseFact]
    public async Task Ca1TheKeyTheSecretsAndTheSealAreNotInTheFolder()
    {
        await using var copied = await CopiedWorld.CreateAsync();

        var everything = string.Concat(await Task.WhenAll(Directory.GetFiles(copied.Folder).Select(file => File.ReadAllTextAsync(file, TestContext.Current.CancellationToken))));
        var key = SyntheticCustody.KeyBytes();

        Assert.DoesNotContain(Convert.ToHexStringLower(key), everything, StringComparison.Ordinal);
        Assert.DoesNotContain(Convert.ToBase64String(key), everything, StringComparison.Ordinal);
        var epoch = SyntheticCustody.SecretOf(1);
        Assert.DoesNotContain(Convert.ToHexStringLower(epoch), everything, StringComparison.Ordinal);
        Assert.DoesNotContain(Convert.ToBase64String(epoch), everything, StringComparison.Ordinal);
        Assert.DoesNotContain(CopiedWorld.Marker, everything, StringComparison.Ordinal);
        Assert.DoesNotContain(copied.Result.Seal!.ToText(), everything, StringComparison.Ordinal);
        Assert.Contains("[redigido]", await File.ReadAllTextAsync(Path.Combine(copied.Folder, "configuration.json"), TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task Ca1TheSealIsOfTheTipAndRecordedInTheChainBeforeTheRowsAreRead()
    {
        await using var copied = await CopiedWorld.CreateAsync();

        var seal = copied.Result.Seal!;
        var recorded = await copied.World.OwnerColumnAsync("SELECT item_id || ':' || step FROM public.custody_event WHERE step = 'seal_recorded'");
        var atSeal = await copied.World.OwnerColumnAsync($"SELECT encode(digest, 'hex') FROM public.custody_event WHERE sequence = {seal.Events}");

        Assert.Equal([$"{seal.Events}:seal_recorded"], recorded);
        Assert.Equal([seal.Digest], atSeal);
        Assert.Equal(2L, seal.Events);
    }

    [DatabaseFact]
    public async Task Ca1AWriteBetweenTwoTablesIsNotInTheCopyBecauseAllOfThemAreReadFromOneSnapshot()
    {
        var token = TestContext.Current.CancellationToken;
        await using var copied = await CopiedWorld.CreateAsync(async (world, table) =>
        {
            if (table == "content.item_text")
            {
                var added = await world.Writer.AddItemTextAsync(1, new NewItemText("texto bruto de depois zqx"), token);
                _ = await world.Writer.AddReversalMapAsync(1, added, "mapa de depois zqx", token);
            }
        });

        var texts = await File.ReadAllLinesAsync(Path.Combine(copied.Folder, "content.item_text.jsonl"), token);
        var maps = await File.ReadAllLinesAsync(Path.Combine(copied.Folder, "content.reversal_map.jsonl"), token);

        var inTheDatabase = await copied.World.InDomainAsync(1, "SELECT count(*)::text FROM content.item_text");

        Assert.Equal("2", inTheDatabase);
        Assert.Equal((1, 1), (texts.Length, maps.Length));
    }

    [DatabaseFact]
    public async Task Ca6NoEventOfReadingIsWrittenByTheCopyThoughItReadsEveryTableOfContent()
    {
        await using var copied = await CopiedWorld.CreateAsync();

        var reads = await copied.World.OwnerColumnAsync("SELECT count(*)::text FROM public.custody_event WHERE step IN ('raw_text_read', 'sanitized_text_read', 'reversal_entries_read')");
        var rows = await File.ReadAllLinesAsync(Path.Combine(copied.Folder, "content.item_text.jsonl"), TestContext.Current.CancellationToken);

        Assert.Equal(["0"], reads);
        _ = Assert.Single(rows);
    }

    [DatabaseFact]
    public async Task FolderThatCannotBeMadeLeavesNoSealInTheChain()
    {
        await using var world = await FidelityWorld.CreateAsync();
        var file = Path.Combine(Path.GetTempPath(), "zqx-copy-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        await File.WriteAllTextAsync(file, "zqx", TestContext.Current.CancellationToken);
        await using var owner = NpgsqlDataSource.Create(world.OwnerConnectionString);
        try
        {
            _ = await Assert.ThrowsAnyAsync<IOException>(
                () => new DataCopy(owner, TimeProvider.System).WriteAsync(Path.Combine(file, "pasta"), CopiedWorld.Configuration, TestContext.Current.CancellationToken));

            Assert.Equal(["0"], await world.OwnerColumnAsync("SELECT count(*)::text FROM public.custody_event"));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [DatabaseFact]
    public async Task FolderThatHoldsSomethingIsRefusedAndNeitherTheSealNorARowIsWritten()
    {
        await using var world = await FidelityWorld.CreateAsync();
        var folder = Path.Combine(Path.GetTempPath(), "zqx-copy-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        _ = Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "other.txt"), "zqx", TestContext.Current.CancellationToken);
        await using var owner = NpgsqlDataSource.Create(world.OwnerConnectionString);
        try
        {
            var result = await new DataCopy(owner, TimeProvider.System).WriteAsync(folder, CopiedWorld.Configuration, TestContext.Current.CancellationToken);

            Assert.Equal(DataCopyOutcome.FolderNotEmpty, result.Outcome);
            Assert.Null(result.Seal);
            Assert.Equal(["0"], await world.OwnerColumnAsync("SELECT count(*)::text FROM public.custody_event"));
            Assert.Equal("other.txt", Assert.Single(Directory.GetFiles(folder).Select(file => Path.GetFileName(file))));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}

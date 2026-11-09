using Core.Persistence.Content;
using Core.Persistence.Content.Reading;
using Core.Persistence.Content.Sealing;
using Core.Persistence.Content.Writing;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Content;

/// <summary>
/// The loss an item declares when its material was accommodated into a form that is not exactly its own (issue 81): the form it received and the codes of what was lost are written in the
/// insert of the item (CA-1), come back with it on any later reading (CA-2), belong to the item and not to the batch (CA-3), are nothing for an item that fits exactly (CA-4), and cannot be left
/// out of an accommodated item, by the type or by the database, the owner included (CA-5).
/// </summary>
[Trait("Category", "Database")]
public sealed class ItemAccommodationTests
{
    private const string Raw = "texto bruto sintetico zqx";
    private const string Sanitized = "texto sanitizado sintetico zqx";
    private const string Insertion =
        "INSERT INTO content.item_text (domain_id, batch_id, raw_text, dek_epoch, dek_wrapped, accommodated_into, losses) "
        + "VALUES (1, 1, decode('a1', 'hex'), 1, decode('c3', 'hex'), @into, @losses) RETURNING id";
    private const string Held = "SELECT coalesce(accommodated_into, '-') || ':' || losses FROM content.item_text WHERE id = @id";
    private const string CheckViolation = "23514";

    [DatabaseFact]
    public async Task ItemAccommodatedIsWrittenWithTheFormItReceivedAndTheCodesOfWhatWasLost()
    {
        await using var world = await ContentWorld.CreateAsync();
        var accommodation = new ItemAccommodation("document", ["author", "instant", "replies_to"]);

        var item = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw, Accommodation: accommodation), TestContext.Current.CancellationToken);
        var result = await ReadAsync(world, item, ContentColumns.RawText);

        Assert.Equal((ContentReadOutcome.Read, Raw, "document:author,instant,replies_to"), (result.Outcome, result.Text, Declared(result)));
        Assert.Equal("document:author,instant,replies_to", await world.OwnerInDomainAsync(1, Held, ("id", item)));
    }

    [DatabaseFact]
    public async Task LossIsStillReachableOnEveryLaterReadingOfTheItem()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(
            1, new NewItemText(Raw, Sanitized, Accommodation: new ItemAccommodation("labeled_sequence", ["author"])), TestContext.Current.CancellationToken);
        _ = await world.Writer.AddItemTextAsync(1, new NewItemText("outro texto sintetico zqx"), TestContext.Current.CancellationToken);

        var first = await ReadAsync(world, item, ContentColumns.RawText);
        var again = await ReadAsync(world, item, ContentColumns.RawText);
        var sanitized = await ReadAsync(world, item, ContentColumns.SanitizedText);
        var elsewhere = await new ProtectedContentReader(world.DataSource, SyntheticCustody.Holding(ContentWorld.Labels).Content, new CustodyChain(world.DataSource))
            .ReadAsync(new ContentRead(CustodyAuthor.System, ContentColumns.RawText, 1, item), TestContext.Current.CancellationToken);

        Assert.All(
            new[] { first, again, sanitized, elsewhere },
            read => Assert.Equal("labeled_sequence:author", Declared(read)));
    }

    [DatabaseFact]
    public async Task TwoItemsOfTheSameBatchAnswerEachForItsOwnLoss()
    {
        await using var world = await ContentWorld.CreateAsync();
        var token = TestContext.Current.CancellationToken;
        var asDocument = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw, Accommodation: new ItemAccommodation("document", ["author", "instant"])), token);
        var asComment = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw, Accommodation: new ItemAccommodation("comment", ["replies_to"])), token);
        var exact = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw), token);

        var document = await ReadAsync(world, asDocument, ContentColumns.RawText);
        var comment = await ReadAsync(world, asComment, ContentColumns.RawText);
        var fits = await ReadAsync(world, exact, ContentColumns.RawText);

        Assert.Equal(
            ("document:author,instant", "comment:replies_to", "-"),
            (Declared(document), Declared(comment), Declared(fits)));
        Assert.Equal(0L, await world.ScalarAsync(
            "SELECT count(*) FROM information_schema.columns WHERE table_schema = 'content' AND table_name = 'ingestion_batch' AND column_name IN ('accommodated_into', 'losses')"));
    }

    [DatabaseFact]
    public async Task ItemThatFitsExactlyHasNoFormReceivedAndNoLoss()
    {
        await using var world = await ContentWorld.CreateAsync();

        var item = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw), TestContext.Current.CancellationToken);
        var result = await ReadAsync(world, item, ContentColumns.RawText);

        Assert.Equal(("-", "-:"), (Declared(result), await world.OwnerInDomainAsync(1, Held, ("id", item))));
    }

    [Fact]
    public void AccommodationCannotBeBuiltWithoutALossOrWithAnythingButCodes()
    {
        Assert.Throws<ArgumentException>(() => new ItemAccommodation("document", []));
        Assert.Throws<ArgumentException>(() => new ItemAccommodation("Document", ["author"]));
        Assert.Throws<ArgumentException>(() => new ItemAccommodation("document", ["o autor foi perdido"]));
        Assert.Throws<ArgumentException>(() => new ItemAccommodation("document", [string.Empty]));
        Assert.Throws<ArgumentNullException>(() => new ItemAccommodation("document", null!));
        Assert.Throws<ArgumentNullException>(() => new ItemAccommodation(null!, ["author"]));
        Assert.Equal(["author"], new ItemAccommodation("document", ["author", "author"]).Losses);
    }

    [Fact]
    public void NoWayOfBuildingTheAccommodationLeavesTheLossesOut()
    {
        var constructors = typeof(ItemAccommodation).GetConstructors();

        Assert.All(constructors, constructor => Assert.Contains(constructor.GetParameters(), parameter => parameter.ParameterType == typeof(IReadOnlyCollection<string>)));
    }

    [DatabaseTheory]
    [InlineData("document", new string[0], "item_text_accommodation_declares_loss")]
    [InlineData(null, new[] { "author" }, "item_text_accommodation_declares_loss")]
    [InlineData("poem", new[] { "author" }, "item_text_accommodated_into_listed")]
    [InlineData("document", new[] { "O autor foi perdido" }, "item_text_losses_are_codes")]
    [InlineData("document", new[] { "author", "" }, "item_text_losses_are_codes")]
    public async Task DatabaseRefusesAnItemAccommodatedWithNoLossAndOneWithLossThatWasNotAccommodatedTheOwnerIncluded(string? into, string[] losses, string constraint)
    {
        await using var world = await ContentWorld.CreateAsync();

        var asApplication = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAsync(1, Insertion, ("into", (object?)into ?? DBNull.Value), ("losses", string.Join(',', losses))));
        var asOwner = await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, Insertion, ("into", (object?)into ?? DBNull.Value), ("losses", string.Join(',', losses))));

        Assert.Equal((CheckViolation, constraint, CheckViolation, constraint), (asApplication.SqlState, asApplication.ConstraintName, asOwner.SqlState, asOwner.ConstraintName));
        Assert.Equal(0L, await world.ScalarAsync("SELECT count(*) FROM content.item_text"));
    }

    [DatabaseFact]
    public async Task WhatAnItemDeclaresNeverChangesAfterTheInsertNotEvenForTheOwner()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(
            1, new NewItemText(Raw, Accommodation: new ItemAccommodation("document", ["author"])), TestContext.Current.CancellationToken);

        var erased = await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(
            1, "UPDATE content.item_text SET accommodated_into = NULL, losses = '' WHERE id = @id RETURNING id", ("id", item)));
        var changed = await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(
            1, "UPDATE content.item_text SET losses = 'instant' WHERE id = @id RETURNING id", ("id", item)));

        Assert.Equal(("CE006", "CE006", "document:author"), (erased.SqlState, changed.SqlState, await world.OwnerInDomainAsync(1, Held, ("id", item))));
    }

    private static string Declared(ContentReadResult read) => read.Accommodation is null ? "-" : $"{read.Accommodation.Form}:{string.Join(',', read.Accommodation.Losses)}";

    private static Task<ContentReadResult> ReadAsync(ContentWorld world, long item, ProtectedColumn column) =>
        world.Reader.ReadAsync(new ContentRead(CustodyAuthor.System, column, 1, item), TestContext.Current.CancellationToken);
}

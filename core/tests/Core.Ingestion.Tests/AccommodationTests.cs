using System.Reflection;

namespace Core.Ingestion.Tests;

/// <summary>
/// The loss declared when a material is accommodated into a form that is not exactly its own (issue 81): the form received and what was lost come out for each unit that lost something (CA-1),
/// each unit answers for its own loss (CA-3), a unit that fits exactly declares none (CA-4) and the declaration cannot be built without at least one loss (CA-5).
/// </summary>
public sealed class AccommodationTests
{
    private const string Marker = "ZQXMARCADOR";

    private static readonly DateTimeOffset Moment = new(2031, 3, 4, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ConversationReadAsDocumentDeclaresTheFormItReceivedAndWhatTheDocumentDoesNotKeep()
    {
        var conversation = new ReadMaterial([new ReadUnit(Marker, "ana", Moment, null, []), new ReadUnit(Marker, "bia", Moment.AddSeconds(5), 0, [])]);

        var declared = Accommodating.Of(conversation, MaterialForm.Document);

        Assert.All(declared, accommodation => Assert.Equal(MaterialForm.Document, accommodation!.Received));
        Assert.Equal(["document:author,instant", "document:author,instant,replies_to"], declared.Select(Shown));
    }

    [Fact]
    public void EachUnitAnswersForItsOwnLossAndNotForOneThatIsCommonToTheMaterial()
    {
        var material = new ReadMaterial([
            new ReadUnit(Marker, "ana", null, null, []),
            new ReadUnit(Marker, null, Moment, null, []),
            new ReadUnit(Marker, null, null, null, [new Passage(0, 3)]),
            new ReadUnit(Marker, null, null, null, []),
        ]);

        var declared = Accommodating.Of(material, MaterialForm.Document);

        Assert.Equal(["document:author", "document:instant", "document:passages", "-"], declared.Select(Shown));
    }

    [Fact]
    public void SeveralUnitsReadAsOneCommentLoseTheThreadOnEachOfThem()
    {
        var thread = new ReadMaterial([new ReadUnit(Marker, "ana", Moment, null, []), new ReadUnit(Marker, "bia", Moment.AddSeconds(5), 0, [])]);

        var declared = Accommodating.Of(thread, MaterialForm.Comment);

        Assert.Equal(["comment:thread", "comment:replies_to,thread"], declared.Select(Shown));
    }

    [Fact]
    public void UnitThatFitsTheFormExactlyDeclaresNoLossInEachOfTheFour()
    {
        var conversation = new ReadMaterial([new ReadUnit(Marker, "ana", Moment, null, []), new ReadUnit(Marker, "bia", Moment.AddSeconds(5), 0, [])]);
        var comment = new ReadMaterial([new ReadUnit(Marker, "ana", Moment, null, [])]);
        var document = new ReadMaterial([new ReadUnit(Marker, null, null, null, []), new ReadUnit(Marker, null, null, null, [])]);
        var sequence = new ReadMaterial([new ReadUnit(Marker, null, null, null, [new Passage(0, 3)])]);

        (ReadMaterial Material, MaterialForm Form)[] fits =
        [
            (conversation, MaterialForm.Conversation),
            (comment, MaterialForm.Comment),
            (document, MaterialForm.Document),
            (sequence, MaterialForm.LabeledSequence),
        ];

        Assert.All(fits, fit => Assert.All(Accommodating.Of(fit.Material, fit.Form), accommodation => Assert.Null(accommodation)));
    }

    [Fact]
    public void AccommodatingIsNotTheDefaultAndAMaterialWithNoUnitDeclaresNothing() =>
        Assert.Empty(Accommodating.Of(new ReadMaterial([]), MaterialForm.Document));

    [Fact]
    public void FormThatIsNotOneOfTheFourOrNoMaterialIsRefused()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Accommodating.Of(new ReadMaterial([]), (MaterialForm)99));
        _ = Assert.Throws<ArgumentNullException>(() => Accommodating.Of(null!, MaterialForm.Document));
    }

    [Fact]
    public void DeclarationCannotBeBuiltWithoutAtLeastOneLossOrWithOneThatIsNotInTheSet()
    {
        _ = Assert.Throws<ArgumentException>(() => new Accommodation(MaterialForm.Document, []));
        _ = Assert.Throws<ArgumentException>(() => new Accommodation(MaterialForm.Document, []));
        _ = Assert.Throws<ArgumentException>(() => new Accommodation(MaterialForm.Document, [(Loss)99]));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new Accommodation((MaterialForm)99, [Loss.Author]));
        _ = Assert.Throws<ArgumentNullException>(() => new Accommodation(MaterialForm.Document, null!));
    }

    [Fact]
    public void NoConstructorOfTheDeclarationLeavesTheLossesOut()
    {
        var constructors = typeof(Accommodation).GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        Assert.All(constructors, constructor => Assert.Contains(constructor.GetParameters(), parameter => parameter.ParameterType == typeof(IReadOnlyCollection<Loss>)));
    }

    [Fact]
    public void LossesComeOrderedAndWithoutRepetitionWhateverTheOrderTheyWereGiven()
    {
        var declared = new Accommodation(MaterialForm.Document, [Loss.RepliesTo, Loss.Author, Loss.RepliesTo, Loss.Instant]);

        Assert.Equal(["author", "instant", "replies_to"], declared.Codes);
    }

    [Fact]
    public void TheTextOfALossComesFromItsCodeAndHoldsNoWordOfTheMaterial()
    {
        var material = new ReadMaterial([new ReadUnit(Marker, "ana", Moment, 0, [new Passage(0, 3)])]);

        var declared = Accommodating.Of(material, MaterialForm.Document)[0]!;

        Assert.Equal(declared.Losses.Select(LossCodes.Words), declared.Words);
        Assert.All(declared.Words, words => Assert.DoesNotContain(Marker, words, StringComparison.OrdinalIgnoreCase));
        Assert.All(Enum.GetValues<Loss>(), loss => Assert.NotEmpty(LossCodes.Words(loss)));
    }

    [Fact]
    public void CodesAreDistinctShapedAsTheDatabaseKeepsThemAndGoBackToTheirLoss()
    {
        var codes = Enum.GetValues<Loss>().Select(loss => LossCodes.Code(loss)).ToList();

        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.All(codes, code => Assert.Matches("^[a-z][a-z0-9_]{0,62}$", code));
        Assert.Equal(Enum.GetValues<Loss>().Cast<Loss?>(), codes.Select(LossCodes.Of));
        Assert.Null(LossCodes.Of("autor"));
        Assert.Null(LossCodes.Of(null));
    }

    [Fact]
    public void FormCodesAreTheFourTheDatabaseListsAndNoOther()
    {
        var codes = Enum.GetValues<MaterialForm>().Select(form => LossCodes.Code(form));

        Assert.Equal(["conversation", "comment", "document", "labeled_sequence"], codes);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => LossCodes.Code((MaterialForm)99));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => LossCodes.Code((Loss)99));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => LossCodes.Words((Loss)99));
    }

    private static string Shown(Accommodation? declared) => declared is null ? "-" : $"{declared.FormCode}:{string.Join(',', declared.Codes)}";
}

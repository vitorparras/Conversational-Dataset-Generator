using Core.Ingestion.Deciding;

namespace Core.Ingestion.Tests.Deciding;

/// <summary>
/// Each unit of the file finds the text that was analysed for it: the texts are asked for in the order of the material, which only the conversation
/// changes, and the items are written in the order of the file.
/// </summary>
public sealed class MaterialTextsTests
{
    [Fact]
    public void EachMessageOfAConversationOutOfOrderFindsItsOwnText()
    {
        ReadUnit[] units = [Units.Message("ZQXTERCEIRA", "ZQXAUTORA", 20), Units.Message("ZQXPRIMEIRA", "ZQXAUTORA", 0), Units.Message("ZQXSEGUNDA", "ZQXAUTORA", 10)];
        var material = Found.Accepted(MaterialForm.Conversation, units);

        var positions = MaterialTexts.PositionsOf(material, new ReadMaterial(units));

        Assert.Equal([2, 0, 1], positions);
        Assert.Equal(units.Select(unit => unit.Text), positions.Select(position => MaterialTexts.Of(material)[position]));
    }

    [Fact]
    public void TheSectionsOfADocumentKeepThePlaceTheyHaveInTheFile()
    {
        ReadUnit[] units = [Units.Plain("ZQXUM"), Units.Plain("ZQXDOIS"), Units.Plain("ZQXTRES")];
        var material = Found.Accepted(MaterialForm.Document, units);

        var positions = MaterialTexts.PositionsOf(material, new ReadMaterial(units));

        Assert.Equal([0, 1, 2], positions);
    }

    [Fact]
    public void SectionsWithMomentsOutOfOrderKeepThePlaceTheyHaveInTheFile()
    {
        // The moments would reorder the units of a conversation to [2, 0, 1]; a document is no conversation, so the order of the file stands.
        ReadUnit[] units = [Units.Message("ZQXTERCEIRA", null, 20), Units.Message("ZQXPRIMEIRA", null, 0), Units.Message("ZQXSEGUNDA", null, 10)];
        var material = Found.Accepted(MaterialForm.Document, units);

        var positions = MaterialTexts.PositionsOf(material, new ReadMaterial(units));

        Assert.Equal([0, 1, 2], positions);
    }

    [Fact]
    public void MissingItemOrUnitsAreRefusedByName()
    {
        ReadUnit[] units = [Units.Plain("ZQXUM")];
        var material = Found.Accepted(MaterialForm.Document, units);

        var withoutItem = Assert.Throws<ArgumentNullException>(() => MaterialTexts.PositionsOf(null!, new ReadMaterial(units)));
        var withoutUnits = Assert.Throws<ArgumentNullException>(() => MaterialTexts.PositionsOf(material, null!));

        Assert.Equal(("material", "read"), (withoutItem.ParamName, withoutUnits.ParamName));
    }

    [Fact]
    public void TheOneUnitOfACommentIsItsOneText()
    {
        ReadUnit[] units = [Units.Message("ZQXCOMENTARIO", "ZQXAUTORA", null)];
        var material = Found.Accepted(MaterialForm.Comment, units);

        var positions = MaterialTexts.PositionsOf(material, new ReadMaterial(units));

        Assert.Equal([0], positions);
    }

    [Fact]
    public void UnitsThatAreNotTheTextsOfTheItemAreRefused()
    {
        ReadUnit[] units = [Units.Plain("ZQXUM"), Units.Plain("ZQXDOIS"), Units.Plain("ZQXTRES")];
        var material = Found.Accepted(MaterialForm.Document, units);

        var refused = Assert.Throws<ArgumentException>(() => MaterialTexts.PositionsOf(material, new ReadMaterial(units[..2])));

        Assert.Equal(("read", "The units read are not one for each text of the item. (Parameter 'read')"), (refused.ParamName, refused.Message));
    }

    [Fact]
    public void UnitsBeyondTheTextsOfTheItemAreRefused()
    {
        ReadUnit[] units = [Units.Plain("ZQXUM"), Units.Plain("ZQXDOIS"), Units.Plain("ZQXTRES")];
        var material = Found.Accepted(MaterialForm.Document, units[..2]);

        var refused = Assert.Throws<ArgumentException>(() => MaterialTexts.PositionsOf(material, new ReadMaterial(units)));

        Assert.Equal("read", refused.ParamName);
    }
}

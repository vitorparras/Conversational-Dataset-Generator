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

        Assert.Equal("read", refused.ParamName);
    }
}

using Core.Persistence.Content;
using Core.Persistence.Content.Writing;

namespace Core.Persistence.Tests.Content;

/// <summary>The words of the refusals of the writing of content and the guard of the texts of an item: what is refused names what was refused and nothing is kept half made.</summary>
public sealed class WritingGuardTests
{
    [Fact]
    public void TheRefusalOfABatchThatIsNotOpenTellsWhoeverOperatesThatNothingWasWritten()
    {
        var refused = new BatchNotOpenException();

        Assert.Equal("O lote não está aberto: nenhum conteúdo é gravado nele; nada foi gravado.", refused.Message);
    }

    [Fact]
    public void ATextWithoutTheRawTextIsRefusedByTheRawParameterWhateverIsDeclaredWithIt()
    {
        ItemAccommodation? none = null;
        var refused = Assert.Throws<ArgumentNullException>(() => new NewItemText(null!, none));

        Assert.Equal("raw", refused.ParamName);
    }
}

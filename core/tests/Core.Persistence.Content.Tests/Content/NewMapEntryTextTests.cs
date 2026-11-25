using Core.Persistence.Content;
using Core.Persistence.Content.Writing;

namespace Core.Persistence.Tests.Content;

/// <summary>The text of an entry of the map names the marker, whether it stands for an author and the destination, and never what the marker replaced.</summary>
public sealed class NewMapEntryTextTests
{
    [Fact]
    public void TheTextNamesTheMarkerTheAuthorAndTheDestinationAndNeverTheOriginal()
    {
        var text = new NewMapEntry("ZQXA-1", "zqx original", true, OccurrenceDestination.Pseudonymize).ToString();

        Assert.Equal("NewMapEntry { Marker = ZQXA-1, Author = True, Destination = Pseudonymize }", text);
        Assert.DoesNotContain("zqx original", text, StringComparison.Ordinal);
    }
}

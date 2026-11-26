using Core.Persistence.Content.Rerun;

namespace Core.Persistence.Tests.Content;

/// <summary>
/// The name the queue keeps for each refusal, the one the migration of the refusals lists, reads back as the same refusal; a name the queue does not keep is refused. The refusal of the last try
/// is written by the statements themselves, so only this test reads its name.
/// </summary>
public sealed class RerunRowsTests
{
    [Theory]
    [InlineData(RerunRefusal.Emptied, "emptied")]
    [InlineData(RerunRefusal.KeyLost, "key_lost")]
    [InlineData(RerunRefusal.Failed, "failed")]
    [InlineData(RerunRefusal.Undecided, "undecided")]
    [InlineData(RerunRefusal.PseudonymWithoutPair, "pseudonym_without_pair")]
    public void EachRefusalIsKeptByItsNameAndReadBack(RerunRefusal refusal, string name) =>
        Assert.Equal((name, refusal), (RerunRows.NameOf(refusal), RerunRows.RefusalOf(name)));

    [Fact]
    public void EveryRefusalHasAName() => Assert.All(Enum.GetValues<RerunRefusal>(), refusal => Assert.Equal(refusal, RerunRows.RefusalOf(RerunRows.NameOf(refusal))));

    [Fact]
    public void NameTheQueueDoesNotKeepIsRefused() => Assert.Equal("name", Assert.Throws<ArgumentOutOfRangeException>(() => RerunRows.RefusalOf("superseded")).ParamName);
}

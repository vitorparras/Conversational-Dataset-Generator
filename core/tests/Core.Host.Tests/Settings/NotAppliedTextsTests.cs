using Core.Host.Screens;
using Core.Host.Settings;

namespace Core.Host.Tests.Settings;

/// <summary>
/// The words of a change that will not be applied (issue 748): every code the core records for a definitive refusal has its own sentence, in words and with no code and no name of a person in it,
/// and a code the version does not know is told as unknown and never as the code itself.
/// </summary>
public sealed class NotAppliedTextsTests
{
    [Fact]
    public void EveryDefinitiveRefusalHasItsOwnSentenceAndNoneIsTheUnknownOne()
    {
        var unknown = ChangeTexts.NotApplied("zqx-codigo-desconhecido");
        var sentences = Enum.GetValues<ApplicationRefusal>().Where(refusal => refusal.IsDefinitive()).Select(refusal => ChangeTexts.NotApplied(refusal.CodeOf())).ToList();

        Assert.Equal(5, sentences.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(unknown, sentences);
        Assert.All(sentences, sentence => Assert.DoesNotContain("_", sentence, StringComparison.Ordinal));
    }

    [Fact]
    public void ACodeTheVersionDoesNotKnowIsToldAsUnknownAndNeverAsTheCode()
    {
        var sentence = ChangeTexts.NotApplied("zqx-codigo-desconhecido");

        Assert.DoesNotContain("zqx", sentence, StringComparison.Ordinal);
        Assert.Equal("O motivo não é conhecido por esta versão.", sentence);
    }
}

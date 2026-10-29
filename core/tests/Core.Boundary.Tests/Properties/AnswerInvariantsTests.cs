using Core.Tests.Support.Harness;
using CsCheck;

namespace Core.Boundary.Tests.Properties;

/// <summary>The validator keeps the invariants of decisão 0011 and requisito 009; a validator that measures the text wrongly falls to its property.</summary>
public sealed class AnswerInvariantsTests
{
    private static AnswerInvariants Over(AnswerValidation validation) => new(PropertyRunner.FromProcess(), validation);

    [Fact]
    [Trait("Kind", "Property")]
    public void AnswerInsideTheTextIsAcceptedWhole() => Over(AnswerValidation.Production).AnswerInsideTheTextIsAcceptedWhole();

    [Fact]
    [Trait("Kind", "Property")]
    public void OneOccurrenceOutsideTheTextRefusesTheAnswer() => Over(AnswerValidation.Production).OneOccurrenceOutsideTheTextRefusesTheAnswer();

    [Fact]
    public void AValidatorCountingUtf16UnitsFailsTheRefusalProperty() =>
        Assert.Throws<CsCheckException>(Over(AnswerValidation.CountingUtf16Units).OneOccurrenceOutsideTheTextRefusesTheAnswer);

    [Fact]
    public void AValidatorReadingTheEndAsInclusiveFailsTheAcceptanceProperty() =>
        Assert.Throws<CsCheckException>(Over(AnswerValidation.ReadingTheEndAsInclusive).AnswerInsideTheTextIsAcceptedWhole);
}

using Core.Persistence.Custody;
using Core.Persistence.People;

namespace Core.Persistence.Tests.People;

/// <summary>The words each refusal gives to whoever operates are the ones declared here: a refusal that says nothing, or the wrong thing, leaves them without the reason.</summary>
public sealed class PersonRefusalsTextTests
{
    private const string ChainBusyWords = "Outra gravação chegou ao mesmo elo da cadeia de custódia antes; nada foi gravado, e o pedido pode ser repetido.";

    [Fact]
    public void EveryRefusalGivesItsOutcomeAndItsWordsAndNoPerson()
    {
        var refusals = new[]
        {
            PersonRefusals.NameTaken(),
            PersonRefusals.ActorUnknown(),
            PersonRefusals.ActorDeactivated(),
            PersonRefusals.PasswordInvalid(),
            PersonRefusals.NotFound(),
            PersonRefusals.Deactivated(),
            PersonRefusals.FunctionUnchanged(),
            PersonRefusals.ChainBusy(),
            PersonRefusals.Judge(new NewPerson(" ", PersonFunctions.Accepted[0]))!,
        };

        Assert.Equal(
            [
                (PersonOutcome.NameTaken, "Já existe uma pessoa com esse nome de uso."),
                (PersonOutcome.ActorUnknown, "A pessoa que age não está cadastrada."),
                (PersonOutcome.ActorDeactivated, "A pessoa que age está desativada."),
                (PersonOutcome.PasswordInvalid, "A senha não pode ser vazia."),
                (PersonOutcome.NotFound, "Não há pessoa com esse identificador."),
                (PersonOutcome.Deactivated, "A pessoa está desativada."),
                (PersonOutcome.FunctionUnchanged, "A pessoa já tem essa função."),
                (PersonOutcome.ChainBusy, ChainBusyWords),
                (PersonOutcome.NameInvalid, "O nome de uso não pode ser vazio nem ter espaço no começo ou no fim."),
            ],
            refusals.Select(refusal => (refusal.Outcome, refusal.Refusal)));
        Assert.All(refusals, refusal => Assert.Null(refusal.PersonId));
    }

    [Fact]
    public void FunctionOutsideThePolicyIsRefusedWithTheOneAskedAndTheAcceptedOnesSeparatedByCommas()
    {
        var refusal = PersonRefusals.NotAccepted("Zqx-Funcao")!;

        Assert.Equal(
            "A função 'Zqx-Funcao' não consta da política de acesso. As aceitas são: Anotação, Arbitragem, Curadoria do acervo, Avaliação, Auditoria, Administração técnica.",
            refusal.Refusal);
    }

    [Fact]
    public void OutcomeOfTheChainThatAChangeOfAPersonCannotMeetIsADefectThatSaysSo()
    {
        var defect = Assert.Throws<ArgumentOutOfRangeException>(() => PersonRefusals.Chained(CustodyResult.Refused((CustodyOutcome)int.MaxValue, "texto"), 3));

        Assert.Contains("The outcome is not one a change of a person can meet.", defect.Message, StringComparison.Ordinal);
    }
}

namespace Core.Persistence.Custody;

/// <summary>The reasons the chain gives for a refusal, in the words of whoever operates.</summary>
internal static class CustodyRefusals
{
    internal static CustodyResult ActorUnknown() =>
        CustodyResult.Refused(CustodyOutcome.ActorUnknown, "A pessoa que age não está cadastrada.");

    internal static CustodyResult ActorDeactivated() =>
        CustodyResult.Refused(CustodyOutcome.ActorDeactivated, "A pessoa que age está desativada.");

    internal static CustodyResult CorrectedNotFound() =>
        CustodyResult.Refused(CustodyOutcome.CorrectedNotFound, "Não há evento com a sequência que se quer corrigir.");

    internal static CustodyResult Conflict() =>
        CustodyResult.Refused(CustodyOutcome.Conflict, "Outra gravação chegou ao mesmo elo da cadeia antes; nada foi gravado, e o pedido pode ser repetido.");

    internal static CustodyResult Immutable() =>
        CustodyResult.Refused(CustodyOutcome.Immutable, "A cadeia de custódia não aceita alterar, remover nem esvaziar um evento; nada foi alterado.");

    internal static CustodyResult StaleVerdict() =>
        CustodyResult.Refused(CustodyOutcome.StaleVerdict, "O veredito de verificação foi feito antes da última falha registrada; nada foi gravado, e a verificação pode ser repetida.");

    internal static CustodyResult ChainBroken() =>
        CustodyResult.Refused(CustodyOutcome.ChainBroken, "A última verificação da cadeia de custódia falhou: só verificação e leitura são gravadas até uma verificação passar; nada foi gravado.");
}

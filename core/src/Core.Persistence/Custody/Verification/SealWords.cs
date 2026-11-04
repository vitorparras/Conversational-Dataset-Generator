namespace Core.Persistence.Custody.Verification;

/// <summary>The words of the seal for whoever operates.</summary>
internal static class SealWords
{
    internal static string Of(SealProblem problem) => problem switch
    {
        SealProblem.Empty => "O texto do selo está vazio.",
        SealProblem.UnknownForm => "O texto não é um selo da forma que esta versão lê.",
        SealProblem.Incomplete => "O texto do selo está incompleto: falta um campo ou parte do código de conferência.",
        SealProblem.Moment => "A data do selo não está escrita como AAAA-MM-DDTHH:MM:SSZ.",
        SealProblem.Count => "A contagem de eventos do selo não é um número inteiro, sem sinal e sem zeros à frente.",
        SealProblem.Digest => "O resumo do selo não tem 64 dígitos hexadecimais em minúsculas.",
        SealProblem.Check => "O código de conferência não confere com o resto do texto: o selo foi alterado ou copiado errado.",
        _ => throw new ArgumentOutOfRangeException(nameof(problem)),
    };

    internal static string Of(SealOutcome outcome) => outcome switch
    {
        SealOutcome.Holds => "A cadeia tem o evento selado, com o mesmo resumo.",
        SealOutcome.CountBelowSealed => "Contagem menor que a selada: a cadeia não tem o evento da sequência selada; o fim da cadeia foi removido ou a base é outra, mais curta.",
        SealOutcome.DigestDiffers => "Resumo diferente do selado: o evento da sequência selada não é o que o selo registra; a base foi substituída por outra.",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
    };
}

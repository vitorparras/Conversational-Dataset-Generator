using Core.Persistence.Custody;

namespace Core.Persistence.ServerGuarantees;

/// <summary>
/// What a step of the chain must leave in the server's audit log: the commands on a table that the steps account for, in the application's role.
/// A table that is not here is seen by the server and listed by the log, but nothing compares it; a step that is not here has to be declared
/// without a table, with its reason, and a test refuses a step that is neither, so that a new step cannot escape the comparison unseen.
/// </summary>
public static class AuditMap
{
    private const string Update = "UPDATE";

    /// <summary>Why the recomputation of a password at sign-in is the one known difference: it writes the credential without an event until issue #71 gives it one.</summary>
    public static string RecomputeWithoutEvent { get; } = "recálculo da senha na entrada (#69) grava a credencial sem evento até a #71";

    /// <summary>The tables the steps account for. Every change of a person also writes a row to person_change, which is why that table lists all four steps.</summary>
    public static IReadOnlyList<AuditExpectation> Expectations { get; } =
    [
        new("public.person", ["INSERT"], [CustodyStep.PersonCreated]),
        new("public.person", [Update], [CustodyStep.PersonDeactivated, CustodyStep.PersonFunctionChanged]),
        new("public.person_credential", ["INSERT", Update], [CustodyStep.PersonPasswordSet], RecomputeWithoutEvent),
        new(
            "public.person_change",
            ["INSERT"],
            [CustodyStep.PersonCreated, CustodyStep.PersonDeactivated, CustodyStep.PersonFunctionChanged, CustodyStep.PersonPasswordSet]),
        new("content.item_text", ["SELECT"], [CustodyStep.RawTextRead, CustodyStep.SanitizedTextRead]),
        new("content.reversal_map", ["SELECT"], [CustodyStep.ReversalEntriesRead]),
        new("content.item_text", [Update], [CustodyStep.ItemEmptied]),
        new("content.reversal_map", [Update], [CustodyStep.ItemEmptied]),
    ];

    /// <summary>The steps that touch no table, each with the reason.</summary>
    public static IReadOnlyDictionary<CustodyStep, string> StepsWithoutTable { get; } = new Dictionary<CustodyStep, string>
    {
        [CustodyStep.Correction] = "é o evento que corrige outro evento: grava só na cadeia e não altera tabela de domínio",
        [CustodyStep.VerifyChain] = "o verificador só lê a cadeia (CustodyStatements.ReadChain) e grava o veredito nela: não toca tabela de domínio",
        [CustodyStep.ItemEmptyingRefused] = "a recusa de esvaziar não altera o conteúdo (o item fica como estava): grava só o evento na cadeia",
        [CustodyStep.EpochRotated] = "a rotação regrava a chave de cada linha antiga, uma instrução por linha, e grava um só evento: os comandos do log não casam com os eventos",
        [CustodyStep.DomainCreated] = "o domínio e os rótulos são escritos com a credencial do dono (não a da aplicação) em tabelas que o mapa não compara: a cadeia guarda o evento",
        [CustodyStep.SealRecorded] = "o selo é lido da cadeia (ChainSealer) e grava só o evento nela: não toca tabela de domínio",
        [CustodyStep.DomainLabelRotated] = "os rótulos da nova geração são escritos com a credencial do dono (não a da aplicação) em tabela que o mapa não compara: a cadeia guarda o evento",
    };
}

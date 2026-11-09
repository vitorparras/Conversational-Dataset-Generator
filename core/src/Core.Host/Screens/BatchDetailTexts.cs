using Core.Persistence.Ingestion;

namespace Core.Host.Screens;

/// <summary>
/// The words of the detail of a batch (#609) for each stable code the ingestion keeps: the form, the destination of a passage and the loss of an accommodation (decision 0024).
/// Every text is fixed by the code alone; one the screen does not know is said by its name, never as an error, and none carries a word of the material.
/// </summary>
internal static class BatchDetailTexts
{
    private static readonly (string Code, string Caption)[] Known =
    {
        ("remove", "Removidos"), ("pseudonymize", "Pseudonimizados"), ("preserve", "Preservados"),
    };

    /// <summary>The three destinations of a passage detected as identifiable (#84), in the order the detail says them, with the code and the words.</summary>
    internal static IReadOnlyList<(string Code, string Caption)> Destinations => Known;

    /// <summary>The name of a form, as a heading of its count.</summary>
    internal static string FormNameOf(string code) => code switch
    {
        "conversation" => "Conversa",
        "comment" => "Comentário",
        "document" => "Documento",
        "labeled_sequence" or "labeled-sequence" => "Sequência anotada",
        _ => $"Forma {code}",
    };

    /// <summary>A form inside a sentence, with its article.</summary>
    internal static string FormOf(string code) => code switch
    {
        "conversation" => "uma conversa",
        "comment" => "um comentário",
        "document" => "um documento",
        "labeled_sequence" or "labeled-sequence" => "uma sequência anotada",
        _ => $"a forma {code}",
    };

    /// <summary>What an accommodation loses, by the code of the loss (#81).</summary>
    internal static string LossOf(string code) => code switch
    {
        "author" => "A autoria por mensagem: o material trazia o autor de cada unidade, e a forma recebida não guarda autor.",
        "instant" => "O instante de cada unidade e, com ele, a ordem no tempo: a forma recebida não guarda quando cada unidade foi dita.",
        "replies_to" => "A estrutura de respostas: o material dizia qual unidade respondia a qual, e a forma recebida não guarda a relação.",
        "passages" => "Os trechos rotulados: o material marcava onde cada rótulo valia no texto, e a forma recebida não guarda trecho.",
        "thread" => "O fio da conversa: o material trazia várias unidades em sequência, lidas como uma manifestação isolada.",
        _ => $"Perda registrada: {code}.",
    };

    /// <summary>Whether the submission declared where the material came from; the origin itself is kept sealed and never shown.</summary>
    internal static string OriginOf(bool declared) => declared ? "Declarada e guardada selada" : "Desconhecida, como declarado no envio";

    /// <summary>Why a batch holds no item, by its state.</summary>
    internal static string NoItemsOf(BatchState state) => state switch
    {
        BatchState.Rejected => "O lote foi rejeitado na entrada, e nenhum item do material foi gravado.",
        BatchState.Open => "O lote ainda está em curso; os itens aparecem aqui quando forem gravados.",
        _ => "O lote foi aceito sem nenhum item gravado.",
    };
}

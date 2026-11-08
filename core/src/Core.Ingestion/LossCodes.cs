namespace Core.Ingestion;

/// <summary>The words kept of a loss and of a form: the code that is stored, and the text that describes the loss, which is always the one of the code.</summary>
public static class LossCodes
{
    private static readonly Dictionary<Loss, (string Code, string Words)> Losses = new()
    {
        [Loss.Author] = ("author", "a autoria por mensagem: o material trazia o autor de cada unidade, e a forma recebida não guarda autor"),
        [Loss.Instant] = ("instant", "o instante de cada unidade e, com ele, a ordem temporal: a forma recebida não guarda quando cada unidade foi dita"),
        [Loss.RepliesTo] = ("replies_to", "a estrutura de respostas: o material dizia qual unidade respondia a qual, e a forma recebida não guarda a relação"),
        [Loss.Passages] = ("passages", "os trechos rotulados: o material marcava onde cada rótulo valia dentro do texto, e a forma recebida não guarda trecho"),
        [Loss.Thread] = ("thread", "o fio da conversa: o material trazia várias unidades em sequência, lidas como uma manifestação isolada"),
    };

    private static readonly Dictionary<MaterialForm, string> Forms = new()
    {
        [MaterialForm.Conversation] = "conversation",
        [MaterialForm.Comment] = "comment",
        [MaterialForm.Document] = "document",
        [MaterialForm.LabeledSequence] = "labeled_sequence",
    };

    /// <summary>The code of a loss, as it is stored beside the item.</summary>
    /// <param name="loss">The loss.</param>
    public static string Code(Loss loss) => Known(loss).Code;

    /// <summary>The text that describes the loss, taken from its code and never from the material.</summary>
    /// <param name="loss">The loss.</param>
    public static string Words(Loss loss) => Known(loss).Words;

    /// <summary>The loss a code stands for; null when the code is not one of the set.</summary>
    /// <param name="code">The code as stored.</param>
    public static Loss? Of(string? code) => Losses.Where(known => known.Value.Code == code).Select(known => (Loss?)known.Key).FirstOrDefault();

    /// <summary>The code of a form, as it is stored beside an item that was accommodated into it.</summary>
    /// <param name="form">The form.</param>
    public static string Code(MaterialForm form) =>
        Forms.TryGetValue(form, out var code) ? code : throw new ArgumentOutOfRangeException(nameof(form), form, "The form is not one of the four.");

    private static (string Code, string Words) Known(Loss loss) =>
        Losses.TryGetValue(loss, out var known) ? known : throw new ArgumentOutOfRangeException(nameof(loss), loss, "The loss is not one of the set.");
}

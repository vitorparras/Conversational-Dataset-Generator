using System.Globalization;
using Core.Persistence.Ingestion;

namespace Core.Host.Screens;

/// <summary>
/// The words of the list of batches (#608): the state of a batch, the motive of a rejection and the size of what the submission brought. A motive is a name the
/// database holds, never free text; one the screen does not know is shown as the name it is, never as an error.
/// </summary>
internal static class BatchTexts
{
    private const int Step = 1024;

    private const string UnitLetters = "KMGT";

    /// <summary>The decimal comma of Portuguese, written out: the host runs with the invariant culture alone.</summary>
    private static readonly NumberFormatInfo Portuguese = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = "." };

    private static readonly Dictionary<string, string> Motives = new(StringComparer.Ordinal)
    {
        { "too_large", "O arquivo passava do tamanho máximo aceito." },
        { "unknown_format", "Esta versão do sistema não lê o formato escolhido." },
        { "format_not_confirmed", "O começo do arquivo não confirmava o formato escolhido." },
        { "unreadable", "O arquivo não pôde ser lido inteiro: estava malformado ou incompleto." },
        { "no_units", "O arquivo não trazia nenhuma unidade de material." },
        { "field_missing", "Uma unidade não trazia algo que a forma escolhida exige." },
        { "field_not_allowed", "Uma unidade trazia algo que a forma escolhida não tem." },
        { "order_not_determined", "Duas mensagens tinham o mesmo instante, e nada no conteúdo dizia qual vem antes." },
        { "passage_outside_text", "Um trecho começava antes do texto, terminava depois dele ou não tinha tamanho." },
        { "conversation_thread", "A forma escolhida é uma manifestação isolada, mas o material trazia uma conversa." },
        { "authorship_by_part", "O material dava autor a uma parte de uma forma cujas partes não têm autor." },
        { "passage_splits_pair", "O limite de um trecho caía no meio de um caractere." },
        { "order_incoherent", "Uma resposta vinha antes da mensagem que ela responde, ou as respostas formavam um ciclo." },
    };

    /// <summary>The state in words, as the list and the filter say it.</summary>
    internal static string StateOf(BatchState state) => state switch
    {
        BatchState.Open => "Em curso",
        BatchState.Accepted => "Aceito",
        BatchState.Rejected => "Rejeitado",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "The state is not one a batch has."),
    };

    /// <summary>The mark of the state, which has its own sign and border besides the colour.</summary>
    internal static StateMark.Tone ToneOf(BatchState state) => state switch
    {
        BatchState.Open => StateMark.Tone.Waiting,
        BatchState.Accepted => StateMark.Tone.Good,
        BatchState.Rejected => StateMark.Tone.Bad,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "The state is not one a batch has."),
    };

    /// <summary>
    /// Why the batch was rejected, in words. The motives are the codes the submission writes (#75): the refusals of the gate and those of the conference of the form.
    /// A motive the screen does not know is said by its name.
    /// </summary>
    internal static string MotiveOf(string motive) => Motives.TryGetValue(motive, out var said) ? said : $"Motivo registrado: {motive}.";

    /// <summary>The warning the submission recorded, in words; the batch was not refused for it. One the screen does not know is said by its name.</summary>
    internal static string WarningOf(string warning) => warning == "name_with_path"
        ? "O nome do arquivo trazia um caminho de pastas; ele foi descartado e o envio seguiu."
        : $"Aviso registrado: {warning}.";

    /// <summary>The size in the largest unit it fills, with one decimal past the bytes, as a person reads it.</summary>
    internal static string SizeOf(long bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        var (size, unit) = ((double)bytes, 0);
        while (size >= Step && unit < UnitLetters.Length)
        {
            (size, unit) = (size / Step, unit + 1);
        }

        return unit == 0 ? $"{bytes.ToString(Portuguese)} B" : $"{size.ToString("0.0", Portuguese)} {UnitLetters[unit - 1]}iB";
    }
}

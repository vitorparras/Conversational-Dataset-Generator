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

    private const int LargestUnit = 4;

    private static readonly CultureInfo Portuguese = CultureInfo.GetCultureInfo("pt-BR");

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

    /// <summary>Why the batch was rejected, in words; a motive the screen does not know is said by its name.</summary>
    internal static string MotiveOf(string motive) => motive switch
    {
        "invalid_rows" => "O arquivo tinha linhas que não puderam ser lidas.",
        "write_failed" => "A gravação do material falhou e nada ficou guardado.",
        _ => $"Motivo registrado: {motive}.",
    };

    /// <summary>The size in the largest unit it fills, with one decimal past the bytes, as a person reads it.</summary>
    internal static string SizeOf(long bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        var (size, unit) = ((double)bytes, 0);
        while (size >= Step && unit < LargestUnit)
        {
            (size, unit) = (size / Step, unit + 1);
        }

        return unit == 0 ? $"{bytes.ToString(Portuguese)} B" : $"{size.ToString("0.0", Portuguese)} {UnitOf(unit)}";
    }

    private static string UnitOf(int unit) => unit switch
    {
        1 => "KiB",
        2 => "MiB",
        3 => "GiB",
        _ => "TiB",
    };
}

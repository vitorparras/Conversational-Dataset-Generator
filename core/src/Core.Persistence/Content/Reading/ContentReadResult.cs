using Core.Persistence.Custody;

namespace Core.Persistence.Content.Reading;

/// <summary>The answer of a reading: how it ended, the text when it was read, and what the chain said when it did not record the event.</summary>
public sealed record ContentReadResult
{
    /// <summary>How the reading ended.</summary>
    public required ContentReadOutcome Outcome { get; init; }

    /// <summary>The content, only when <see cref="ContentReadOutcome.Read"/>.</summary>
    public string? Text { get; init; }

    /// <summary>The refusal of the chain, only when <see cref="ContentReadOutcome.NotRecorded"/>.</summary>
    public CustodyResult? Refusal { get; init; }

    /// <summary>The label that is missing, for <see cref="ContentReadOutcome.LabelMissing"/>, or the scope of the loss declared, for <see cref="ContentReadOutcome.KeyLossDeclared"/>.</summary>
    public string? Label { get; init; }

    /// <summary>The row the reading found, so that the next reading of the first row can look after it; 0 when it found none.</summary>
    public long RowId { get; init; }
}

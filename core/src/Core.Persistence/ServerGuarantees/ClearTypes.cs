using Core.Persistence.Content;

namespace Core.Persistence.ServerGuarantees;

/// <summary>The type the database gives each kind of column stored in clear, so that a column listed as a date cannot hold text.</summary>
internal static class ClearTypes
{
    internal static string Of(ClearKind kind) => kind switch
    {
        ClearKind.Identifier => "bigint",
        ClearKind.Domain => "integer",
        ClearKind.Moment => "timestamp with time zone",
        ClearKind.Epoch => "integer",
        ClearKind.Transaction => "xid8",
        _ => Text(kind),
    };

    /// <summary>The kinds that are words of a closed form or the pseudonymized text, which the database holds as text.</summary>
    private static string Text(ClearKind kind) => kind switch
    {
        ClearKind.PseudonymizedText => "text",
        ClearKind.Status => "text",
        ClearKind.Motive => "text",
        ClearKind.Condition => "text",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}

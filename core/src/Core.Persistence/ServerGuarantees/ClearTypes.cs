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
        ClearKind.PseudonymizedText => "text",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}

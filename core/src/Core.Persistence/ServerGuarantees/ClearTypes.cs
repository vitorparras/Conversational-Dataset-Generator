using System.Collections.Frozen;
using Core.Persistence.Content;

namespace Core.Persistence.ServerGuarantees;

/// <summary>The type the database gives each kind of column stored in clear, so that a column listed as a date cannot hold text.</summary>
internal static class ClearTypes
{
    private static readonly FrozenDictionary<ClearKind, string> Types = new Dictionary<ClearKind, string>
    {
        [ClearKind.Identifier] = "bigint",
        [ClearKind.Domain] = "integer",
        [ClearKind.Moment] = "timestamp with time zone",
        [ClearKind.Epoch] = "integer",
        [ClearKind.Status] = "text",
        [ClearKind.Motive] = "text",
        [ClearKind.Transaction] = "xid8",
        [ClearKind.Steps] = "text",
        [ClearKind.Counts] = "integer[]",
        [ClearKind.Flag] = "boolean",
    }.ToFrozenDictionary();

    internal static string Of(ClearKind kind) =>
        Types.TryGetValue(kind, out var type) ? type : throw new ArgumentOutOfRangeException(nameof(kind));
}

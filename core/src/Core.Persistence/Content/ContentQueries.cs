using System.Collections.Frozen;

namespace Core.Persistence.Content;

/// <summary>
/// Every query the system exposes over content, by name, so that a test can run each one with no domain declared and demand that it returns
/// nothing. It starts empty because no content table exists yet; each issue that exposes a query over content adds it here.
/// </summary>
public static class ContentQueries
{
    /// <summary>The queries over content, as the statement each one runs.</summary>
    public static IReadOnlyDictionary<string, string> All { get; } = FrozenDictionary<string, string>.Empty;
}

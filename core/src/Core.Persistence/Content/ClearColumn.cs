namespace Core.Persistence.Content;

/// <summary>A column of the content schema that is stored in clear, and what it holds.</summary>
/// <param name="Table">The table, without the schema.</param>
/// <param name="Name">The column.</param>
/// <param name="Kind">What the column holds.</param>
public sealed record ClearColumn(string Table, string Name, ClearKind Kind);

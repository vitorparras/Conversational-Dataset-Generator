using Core.Platform.Keys;

namespace Core.Persistence.Content;

/// <summary>A column of the content schema whose value is stored sealed, and the purpose whose key seals it.</summary>
/// <param name="Table">The table, without the schema.</param>
/// <param name="Name">The column.</param>
/// <param name="Purpose">The purpose of the key; the sanitized text shares the purpose of the raw text, as decision 0045 says.</param>
public sealed record ProtectedColumn(string Table, string Name, LabelPurpose Purpose);

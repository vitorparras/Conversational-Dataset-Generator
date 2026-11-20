namespace Core.Ingestion.Sanitizing;

/// <summary>What the rerun of the sanitization made of the raw text of an item, and whether the text is, ordinal, the one the item holds.</summary>
/// <param name="Sanitized">The sanitized text, its trail and the report of each step.</param>
/// <param name="Identical">Whether the text is the one the item holds; then it need not be written again.</param>
public sealed record RerunText(SanitizedText Sanitized, bool Identical)
{
    /// <summary>The version, the counts and whether it is identical; never the text.</summary>
    public override string ToString() => $"RerunText {{ Sanitized = {Sanitized}, Identical = {Identical} }}";
}

namespace Core.Ingestion.Sanitizing;

/// <summary>What the rerun of the sanitization made of the raw text of an item, or why it refused, and whether the text is, ordinal, the one the item holds.</summary>
/// <param name="Result">The sanitized text, or the refusal when the destinations of the item were never decided.</param>
/// <param name="Identical">Whether the text is the one the item holds; then it need not be written again. Never when refused.</param>
public sealed record RerunText(SanitizationResult Result, bool Identical)
{
    /// <summary>The version, the counts or the refusal, and whether it is identical; never the text.</summary>
    public override string ToString() => $"RerunText {{ Sanitized = {Result.Sanitized}, Refusal = {Result.Refusal}, Identical = {Identical} }}";
}

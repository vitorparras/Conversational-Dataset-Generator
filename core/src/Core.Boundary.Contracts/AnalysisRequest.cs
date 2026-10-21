using Core.Platform.Logging;

namespace Core.Boundary.Contracts;

/// <summary>What the core sends to the language service: the text, and nothing that says whose it is or where it came from.</summary>
/// <param name="Text">The text to analyse, before sanitization.</param>
/// <param name="Language">The language of the text.</param>
/// <param name="Reference">An opaque reference to the request, which only the core can map back to the material.</param>
/// <param name="ContractVersion">The version of the contract the core speaks.</param>
public sealed record AnalysisRequest([property: Material] string Text, string Language, string Reference, string ContractVersion)
{
    /// <summary>The reference, language, contract version and a size in UTF-16 units for diagnosis only, never a contract offset; never the text.</summary>
    public override string ToString() =>
        $"AnalysisRequest {{ Reference = {Reference}, Language = {Language}, ContractVersion = {ContractVersion}, TextChars = {Text.Length} }}";
}

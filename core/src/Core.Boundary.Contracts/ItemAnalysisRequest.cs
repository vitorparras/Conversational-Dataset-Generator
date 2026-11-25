namespace Core.Boundary.Contracts;

/// <summary>What the core sends to have the coreference of a whole item resolved: every text of it, in the order of the item, and nothing that says whose it is.</summary>
/// <param name="Texts">The texts of the item, each with its author, in the order in which the core keeps them.</param>
/// <param name="Language">The language of the texts.</param>
/// <param name="Reference">An opaque reference to the request, which only the core can map back to the material.</param>
/// <param name="ContractVersion">The version of the contract the core speaks.</param>
public sealed record ItemAnalysisRequest(IReadOnlyList<ItemText> Texts, string Language, string Reference, string ContractVersion)
{
    /// <summary>The reference, language, contract version and how many texts; never a text nor an author.</summary>
    public override string ToString() =>
        $"ItemAnalysisRequest {{ Reference = {Reference}, Language = {Language}, ContractVersion = {ContractVersion}, Texts = {Texts.Count} }}";
}

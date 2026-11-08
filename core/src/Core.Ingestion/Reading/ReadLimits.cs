namespace Core.Ingestion.Reading;

/// <summary>What a reader declares it will read and no more: past any of these the material is refused, and nothing is followed, expanded or kept going.</summary>
/// <param name="MaxDepth">How deep the structure may nest, the root counted as the first level.</param>
/// <param name="MaxUnits">How many units a material may hold.</param>
/// <param name="MaxTextBytes">How many bytes, in UTF-8, the text of one unit may have.</param>
/// <param name="HeadBytes">How many of the first bytes of the content the reader needs to recognise its format.</param>
public sealed record ReadLimits(int MaxDepth, int MaxUnits, int MaxTextBytes, int HeadBytes);

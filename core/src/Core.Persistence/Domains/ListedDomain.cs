namespace Core.Persistence.Domains;

/// <summary>
/// A domain as the list shows it. The model keeps no version of the taxonomy or of the dimensions of a domain yet, so both are absent (null) and never a made-up value;
/// the day the model keeps them, the list fills them in and nothing that reads it changes.
/// </summary>
/// <param name="Id">The identifier of the domain.</param>
/// <param name="Name">The name of the domain.</param>
/// <param name="CreatedAt">When the domain was created.</param>
/// <param name="TaxonomyVersion">The version of the taxonomy loaded for the domain; null while the model keeps none.</param>
/// <param name="DimensionsVersion">The version of the dimensions loaded for the domain; null while the model keeps none.</param>
public sealed record ListedDomain(int Id, string Name, DateTimeOffset CreatedAt, string? TaxonomyVersion, string? DimensionsVersion);

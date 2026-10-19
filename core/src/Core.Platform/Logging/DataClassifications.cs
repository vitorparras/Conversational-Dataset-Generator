using Microsoft.Extensions.Compliance.Classification;

namespace Core.Platform.Logging;

/// <summary>The three kinds of data the platform erases from every log line: material text, a person's identity and a secret.</summary>
public static class DataClassifications
{
    /// <summary>The taxonomy every classification of this platform belongs to.</summary>
    public static string TaxonomyName { get; } = "Sensitive";

    /// <summary>Text of the material being processed.</summary>
    public static DataClassification Material { get; } = new(TaxonomyName, nameof(Material));

    /// <summary>Anything that identifies a person.</summary>
    public static DataClassification Identity { get; } = new(TaxonomyName, nameof(Identity));

    /// <summary>A key, token, password or any other secret.</summary>
    public static DataClassification Secret { get; } = new(TaxonomyName, nameof(Secret));
}

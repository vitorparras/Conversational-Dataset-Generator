using Microsoft.Extensions.Compliance.Classification;

namespace Core.Platform.Logging;

/// <summary>
/// The three kinds of sensitive data (issue 34). A value marked with any of them is erased on the
/// way to every log destination, whoever wrote the call.
/// </summary>
public static class DataClassifications
{
    /// <summary>The name of the scheme the three classifications belong to.</summary>
    public const string SchemeName = "Core";

    /// <summary>Text of a material, or any part of it.</summary>
    public static DataClassification Material { get; } = new(SchemeName, nameof(Material));

    /// <summary>Anything that identifies a person.</summary>
    public static DataClassification Identity { get; } = new(SchemeName, nameof(Identity));

    /// <summary>A key, credential or token.</summary>
    public static DataClassification Secret { get; } = new(SchemeName, nameof(Secret));

    /// <summary>All three, for registering the redactor.</summary>
    public static IReadOnlyList<DataClassification> All { get; } = [Material, Identity, Secret];
}

/// <summary>Marks text of a material.</summary>
public sealed class MaterialAttribute : DataClassificationAttribute
{
    /// <summary>Classifies the target as <see cref="DataClassifications.Material"/>.</summary>
    public MaterialAttribute()
        : base(DataClassifications.Material)
    {
    }
}

/// <summary>Marks anything that identifies a person.</summary>
public sealed class IdentityAttribute : DataClassificationAttribute
{
    /// <summary>Classifies the target as <see cref="DataClassifications.Identity"/>.</summary>
    public IdentityAttribute()
        : base(DataClassifications.Identity)
    {
    }
}

/// <summary>Marks a key, credential or token.</summary>
public sealed class SecretAttribute : DataClassificationAttribute
{
    /// <summary>Classifies the target as <see cref="DataClassifications.Secret"/>.</summary>
    public SecretAttribute()
        : base(DataClassifications.Secret)
    {
    }
}

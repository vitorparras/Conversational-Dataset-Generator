using Microsoft.Extensions.Compliance.Classification;

namespace Core.Platform.Logging;

/// <summary>Marks a value as text of the material being processed; it is erased from every log line it crosses.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class MaterialAttribute : DataClassificationAttribute
{
    /// <summary>Classifies the member as <see cref="DataClassifications.Material"/>.</summary>
    public MaterialAttribute()
        : base(DataClassifications.Material)
    {
    }
}

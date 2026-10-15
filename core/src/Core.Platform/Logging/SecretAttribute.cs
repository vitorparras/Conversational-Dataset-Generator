using Microsoft.Extensions.Compliance.Classification;

namespace Core.Platform.Logging;

/// <summary>Marks a value as a secret; it is erased from every log line it crosses.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class SecretAttribute : DataClassificationAttribute
{
    /// <summary>Classifies the member as <see cref="DataClassifications.Secret"/>.</summary>
    public SecretAttribute()
        : base(DataClassifications.Secret)
    {
    }
}

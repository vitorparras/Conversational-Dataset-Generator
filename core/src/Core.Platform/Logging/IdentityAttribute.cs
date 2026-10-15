using Microsoft.Extensions.Compliance.Classification;

namespace Core.Platform.Logging;

/// <summary>Marks a value as the identity of a person; it is erased from every log line it crosses.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class IdentityAttribute : DataClassificationAttribute
{
    /// <summary>Classifies the member as <see cref="DataClassifications.Identity"/>.</summary>
    public IdentityAttribute()
        : base(DataClassifications.Identity)
    {
    }
}

namespace Core.Platform.Configuration;

/// <summary>The configuration in force as it may be shown: every declared key with its origin, sensitive values redacted.</summary>
internal sealed class EffectiveConfiguration
{
    /// <summary>What stands in place of a sensitive value.</summary>
    public const string Redacted = "[redigido]";

    public EffectiveConfiguration(IReadOnlyList<ResolvedValue> resolved)
    {
        Entries = [.. resolved.Select(Shown)];
    }

    /// <summary>Every declared key, in declaration order.</summary>
    public IReadOnlyList<EffectiveEntry> Entries { get; }

    private static EffectiveEntry Shown(ResolvedValue resolved) =>
        new(resolved.Declared.Path, resolved.Declared.Key.IsSensitive ? Redacted : resolved.Value, resolved.Origin);
}

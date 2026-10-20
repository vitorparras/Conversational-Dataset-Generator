namespace Core.Platform.Configuration;

/// <summary>One key a section accepts: how it must be declared, what it accepts and whether its value is a secret.</summary>
public sealed record ConfigurationKey
{
    private ConfigurationKey(string name, KeyObligation obligation, string? defaultValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Contains(':', StringComparison.Ordinal))
        {
            throw new ArgumentException($"Key name '{name}' must not contain ':'.", nameof(name));
        }

        Name = name;
        Obligation = obligation;
        DefaultValue = defaultValue;
    }

    internal string Name { get; }

    internal KeyObligation Obligation { get; }

    /// <summary>The value taken when no source declares the key; null for a required key and for an optional key without one.</summary>
    internal string? DefaultValue { get; }

    /// <summary>The closed set of accepted values; null when any non-empty text is accepted.</summary>
    internal IReadOnlyList<string>? AcceptedValues { get; private init; }

    internal bool IsSensitive { get; private init; }

    internal bool IsRequired => Obligation is not KeyObligation.Optional;

    /// <summary>A key some source must declare.</summary>
    public static ConfigurationKey Required(string name) => new(name, KeyObligation.Required, defaultValue: null);

    /// <summary>A key that holds a protection: required, and nothing can give it a default.</summary>
    public static ConfigurationKey Protection(string name) => new(name, KeyObligation.Protection, defaultValue: null);

    /// <summary>A key no source has to declare and that has no default: absent, it has no value.</summary>
    public static ConfigurationKey Optional(string name) => new(name, KeyObligation.Optional, defaultValue: null);

    /// <summary>A key that takes the default when no source declares it.</summary>
    public static ConfigurationKey WithDefault(string name, string defaultValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultValue);
        return new(name, KeyObligation.Optional, defaultValue);
    }

    /// <summary>The same key, accepting only the values given.</summary>
    public ConfigurationKey Accepting(params IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0 || values.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("The accepted set needs at least one value, and none may be empty.", nameof(values));
        }

        if (DefaultValue is not null && !values.Contains(DefaultValue, StringComparer.Ordinal))
        {
            throw new ArgumentException($"The default of '{Name}' is not among the accepted values.", nameof(values));
        }

        return this with { AcceptedValues = [.. values] };
    }

    /// <summary>The same key, its value never shown.</summary>
    public ConfigurationKey AsSensitive() => this with { IsSensitive = true };

    internal bool Accepts(string value) => AcceptedValues is null || AcceptedValues.Contains(value, StringComparer.Ordinal);
}

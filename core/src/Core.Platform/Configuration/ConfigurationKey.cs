namespace Core.Platform.Configuration;

/// <summary>One key a section accepts: how it must be declared, what it accepts and whether its value is a secret.</summary>
internal sealed record ConfigurationKey
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

    public string Name { get; }

    public KeyObligation Obligation { get; }

    /// <summary>The value taken when no source declares the key; null for a required key.</summary>
    public string? DefaultValue { get; }

    /// <summary>The closed set of accepted values; null when any non-empty text is accepted.</summary>
    public IReadOnlyList<string>? AcceptedValues { get; private init; }

    public bool IsSensitive { get; private init; }

    public bool IsRequired => Obligation is not KeyObligation.Optional;

    public static ConfigurationKey Required(string name) => new(name, KeyObligation.Required, defaultValue: null);

    public static ConfigurationKey Protection(string name) => new(name, KeyObligation.Protection, defaultValue: null);

    public static ConfigurationKey WithDefault(string name, string defaultValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultValue);
        return new(name, KeyObligation.Optional, defaultValue);
    }

    public ConfigurationKey Accepting(params IReadOnlyList<string> values)
    {
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

    public ConfigurationKey AsSensitive() => this with { IsSensitive = true };

    public bool Accepts(string value) => AcceptedValues is null || AcceptedValues.Contains(value, StringComparer.Ordinal);
}

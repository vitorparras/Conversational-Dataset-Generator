using System.Globalization;
using System.Net;

namespace Core.Platform.Configuration;

/// <summary>One key a section accepts, and what it accepts in it.</summary>
public sealed record ConfigurationKey
{
    private ConfigurationKey(
        string name,
        bool required,
        string? defaultValue,
        bool protects,
        bool sensitive,
        IReadOnlyList<string>? acceptedValues,
        ValueFormat format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Contains(':', StringComparison.Ordinal))
        {
            throw new ArgumentException($"key name '{name}' must not contain ':'", nameof(name));
        }

        Name = name;
        IsRequired = required;
        DefaultValue = defaultValue;
        Protects = protects;
        Sensitive = sensitive;
        AcceptedValues = acceptedValues;
        Format = format;

        if (defaultValue is not null && Check(defaultValue) is not null)
        {
            throw new ArgumentException(
                $"the default of '{name}' is not a value the key accepts", nameof(defaultValue));
        }
    }

    /// <summary>The key name inside its section.</summary>
    public string Name { get; }

    /// <summary>Whether loading refuses to start when no source declares the key.</summary>
    public bool IsRequired { get; }

    /// <summary>The value used when no source declares the key; null for a required key.</summary>
    public string? DefaultValue { get; }

    /// <summary>Whether the key holds a protection. Such a key is required and has no default.</summary>
    public bool Protects { get; }

    /// <summary>Whether the value is a secret, redacted wherever the configuration is shown.</summary>
    public bool Sensitive { get; }

    /// <summary>The closed set of accepted values, or null when the format alone decides.</summary>
    public IReadOnlyList<string>? AcceptedValues { get; }

    /// <summary>The shape the value must have.</summary>
    public ValueFormat Format { get; }

    /// <summary>A key that some source must declare.</summary>
    public static ConfigurationKey Required(
        string name,
        ValueFormat format = ValueFormat.Text,
        IReadOnlyList<string>? acceptedValues = null,
        bool sensitive = false) =>
        new(name, required: true, defaultValue: null, protects: false, sensitive, acceptedValues, format);

    /// <summary>A key that holds a protection: required, and there is no way to give it a default.</summary>
    public static ConfigurationKey Protection(
        string name,
        ValueFormat format = ValueFormat.Text,
        IReadOnlyList<string>? acceptedValues = null,
        bool sensitive = false) =>
        new(name, required: true, defaultValue: null, protects: true, sensitive, acceptedValues, format);

    /// <summary>A key with a legitimate default, used when no source declares it.</summary>
    public static ConfigurationKey WithDefault(
        string name,
        string defaultValue,
        ValueFormat format = ValueFormat.Text,
        IReadOnlyList<string>? acceptedValues = null,
        bool sensitive = false)
    {
        ArgumentNullException.ThrowIfNull(defaultValue);
        return new(name, required: false, defaultValue, protects: false, sensitive, acceptedValues, format);
    }

    /// <summary>What the key accepts, as the operator reads it: the closed set, or the shape of the value.</summary>
    public string Accepts => AcceptedValues is not null ? string.Join(", ", AcceptedValues) : FormatDescription(Format);

    /// <summary>Says what the key would accept when it refuses <paramref name="value"/>; null when it accepts.</summary>
    public string? Check(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (AcceptedValues is not null && !AcceptedValues.Contains(value, StringComparer.Ordinal))
        {
            return Accepts;
        }

        var fits = Format switch
        {
            ValueFormat.Text => true,
            ValueFormat.Port => IsPort(value),
            ValueFormat.IpAddress => IPAddress.TryParse(value, out _),
            _ => throw new InvalidOperationException($"unknown format {Format}"),
        };
        return fits ? null : FormatDescription(Format);
    }

    private static string FormatDescription(ValueFormat format) => format switch
    {
        ValueFormat.Text => "qualquer texto",
        ValueFormat.Port => "um número de porta entre 0 e 65535",
        ValueFormat.IpAddress => "um endereço IP",
        _ => throw new InvalidOperationException($"unknown format {format}"),
    };

    private static bool IsPort(string value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port <= 65535;
}

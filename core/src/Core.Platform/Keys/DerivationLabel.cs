using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Core.Platform.Keys;

/// <summary>A derivation label as the format <c>&lt;purpose&gt;/&lt;domain id&gt;/v&lt;n&gt;</c> says it: a closed purpose, the domain's whole positive id and a version from 1.</summary>
public sealed record DerivationLabel
{
    private const char Separator = '/';
    private const char VersionMark = 'v';

    /// <summary>What the key is for.</summary>
    public LabelPurpose Purpose { get; }

    /// <summary>The id of the domain the key belongs to.</summary>
    public int DomainId { get; }

    /// <summary>The version of the key, which a rotation increments.</summary>
    public int Version { get; }

    /// <summary>The label, as written in the custody file and in the envelope's header.</summary>
    public string Text => $"{Name(Purpose)}{Separator}{DomainId}{Separator}{VersionMark}{Version}";

    /// <summary>The label of the purpose, the domain and the version; an id or a version below 1, or a purpose outside the set, is a defect of the caller.</summary>
    public DerivationLabel(LabelPurpose purpose, int domainId, int version)
    {
        _ = Name(purpose);
        ArgumentOutOfRangeException.ThrowIfLessThan(domainId, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);
        Purpose = purpose;
        DomainId = domainId;
        Version = version;
    }

    /// <summary>Reads the text only if it is exactly a label as <see cref="Text"/> writes it; anything else, such as a padded number, an unknown purpose or a sign, is not one.</summary>
    public static bool TryParse(string? text, [NotNullWhen(true)] out DerivationLabel? label)
    {
        label = null;
        var parts = text?.Split(Separator);
        if (parts is null || parts.Length != 3 || !parts[^1].StartsWith(VersionMark))
        {
            return false;
        }

        if (!TryPurpose(parts[0], out var purpose) || !TryNumber(parts[1], out var domainId) || !TryNumber(parts[^1][1..], out var versionNumber))
        {
            return false;
        }

        label = new DerivationLabel(purpose, domainId, versionNumber);
        return true;
    }

    private static bool TryPurpose(string name, out LabelPurpose purpose)
    {
        var found = Enum.GetValues<LabelPurpose>().Cast<LabelPurpose?>().FirstOrDefault(candidate => Name(candidate!.Value) == name);
        purpose = found.GetValueOrDefault();
        return found.HasValue;
    }

    private static bool TryNumber(string digits, out int number)
    {
        number = 0;
        return digits.Length > 0 && digits[0] is >= '1' and <= '9' && digits.All(char.IsAsciiDigit) && int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out number);
    }

    private static string Name(LabelPurpose purpose) => purpose switch
    {
        LabelPurpose.RawText => "texto-bruto",
        LabelPurpose.ReversalMap => "mapa-reversao",
        _ => throw new ArgumentOutOfRangeException(nameof(purpose)),
    };
}

using System.Globalization;
using Core.Persistence.People;
using Core.Platform;
using Core.Platform.Configuration;
using ConfigurationSection = Core.Platform.Configuration.ConfigurationSection;

namespace Core.Host;

/// <summary>
/// The work factor of the derivation of passwords: declared by every installation, with no default, since a number chosen here would be the one every installation runs with.
/// A real installation accepts it from <see cref="RealFloor" /> to <see cref="Ceiling" />, both included; a demonstration, which is declared and receives no real material, from
/// <see cref="DemonstrationFloor" /> to the same ceiling. The three numbers live here and nowhere else (decision 0047).
/// </summary>
internal static class PasswordConfiguration
{
    internal const string WorkFactorPath = SectionName + ":" + WorkFactorKey;

    /// <summary>The lowest factor of a real installation: the OWASP recommendation for PBKDF2-HMAC-SHA256.</summary>
    internal const int RealFloor = 600_000;

    /// <summary>The lowest factor of a demonstration: any positive number, so that what only exercises the derivation does not pay for it.</summary>
    internal const int DemonstrationFloor = 1;

    /// <summary>The highest factor of any installation: a slower entry would let one request take the server down.</summary>
    internal const int Ceiling = 10_000_000;

    private const string SectionName = "Passwords";
    private const string WorkFactorKey = "WorkFactor";
    private const string Accepted = "número inteiro de iterações da derivação";

    internal static ConfigurationSection Section { get; } = new(SectionName, [ConfigurationKey.Required(WorkFactorKey)]);

    /// <summary>The same section for the migration mode, which derives no password: the factor may be given, and is then judged like any other start, but is not required.</summary>
    internal static ConfigurationSection SectionWhenMigrating { get; } = new(SectionName, [ConfigurationKey.Optional(WorkFactorKey)]);

    /// <summary>
    /// The refusal of a work factor declared that is not a whole number inside the interval of the installation, naming the key and the limit that was crossed; none when it is inside,
    /// or when it is not declared and the mode does not require it. An installation that did not declare its kind (the migration mode) is held to the real one.
    /// </summary>
    internal static IReadOnlyList<ConfigurationProblem> Refusals(IReadOnlyDictionary<string, string> values)
    {
        if (!values.TryGetValue(WorkFactorPath, out var declared))
        {
            return [];
        }

        var real = InstallationConfiguration.Of(values).Kind == InstallationKind.Real;
        var floor = real ? RealFloor : DemonstrationFloor;
        var accepted = WorkFactor(declared) switch
        {
            null => $"{Accepted}, de {floor} a {Ceiling}",
            var factor when factor < floor && real => $"{Accepted}, no mínimo {RealFloor} na instalação real",
            var factor when factor < floor => $"{Accepted}, no mínimo {DemonstrationFloor}",
            var factor when factor > Ceiling => $"{Accepted}, no máximo {Ceiling}",
            _ => null,
        };
        return accepted is null ? [] : [new ConfigurationProblem(ConfigurationProblemKind.ValueNotAccepted, WorkFactorPath, declared, [accepted])];
    }

    /// <summary>The derivation that runs with the work factor the configuration gave; the configuration has been judged before.</summary>
    internal static PasswordDerivation Derivation(IReadOnlyDictionary<string, string> values) => new(WorkFactor(values[WorkFactorPath])!.Value);

    private static int? WorkFactor(string declared) =>
        int.TryParse(declared, NumberStyles.None, CultureInfo.InvariantCulture, out var factor) ? factor : null;
}

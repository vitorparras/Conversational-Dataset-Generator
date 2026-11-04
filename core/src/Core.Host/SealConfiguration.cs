using Core.Platform.Configuration;
using ConfigurationSection = Core.Platform.Configuration.ConfigurationSection;

namespace Core.Host;

/// <summary>
/// Where the installation keeps the seal of its chain of custody and where it is sent, declared by every installation that serves and with no default, since a default would let an
/// installation pass for one that keeps its seal outside the control of the database. The keys are <c>Custody:SealSituation</c> (<c>External</c>, <c>Internal</c> or <c>Manual</c>)
/// and <c>Custody:SealDestination</c>, an https address, which the first two require and the manual one does not have.
/// </summary>
internal static class SealConfiguration
{
    internal const string SituationPath = SectionName + ":" + SituationKey;

    internal const string DestinationPath = SectionName + ":" + DestinationKey;

    private const string SectionName = "Custody";
    private const string SituationKey = "SealSituation";
    private const string DestinationKey = "SealDestination";
    private const string DatabaseHostPath = "Database:Host";
    private const string Manual = nameof(SealSituation.Manual);

    internal static ConfigurationSection Section { get; } = new(SectionName, KeysOf(ConfigurationKey.Required(SituationKey)));

    /// <summary>The same section for the modes that serve nothing: the situation may be given, and is then checked, but is not required.</summary>
    internal static ConfigurationSection SectionWhenMigrating { get; } = new(SectionName, KeysOf(ConfigurationKey.Optional(SituationKey)));

    /// <summary>
    /// What the destination declared breaks: none for a situation not declared, which the mode requires or not. The manual situation has no destination,
    /// and the others need one the host can accept.
    /// </summary>
    internal static IReadOnlyList<ConfigurationProblem> Refusals(IReadOnlyDictionary<string, string> values)
    {
        var problems = new List<ConfigurationProblem>();
        if (!values.TryGetValue(SituationPath, out var situation))
        {
            return problems;
        }

        var destination = values.GetValueOrDefault(DestinationPath);
        var reason = string.Equals(situation, Manual, StringComparison.Ordinal) ? ManualReason(destination) : DestinationReason(destination, values.GetValueOrDefault(DatabaseHostPath));
        if (reason is not null)
        {
            problems.Add(Refused(destination, reason));
        }

        return problems;
    }

    /// <summary>The declaration the configuration gave; the configuration has been judged before.</summary>
    internal static SealDeclaration Declaration(IReadOnlyDictionary<string, string> values)
    {
        var situation = Enum.Parse<SealSituation>(values[SituationPath]);
        return new SealDeclaration(situation, situation == SealSituation.Manual ? null : new Uri(values[DestinationPath]));
    }

    private static ConfigurationKey[] KeysOf(ConfigurationKey situation)
    {
        var keys = new ConfigurationKey[2];
        keys[0] = situation.Accepting(Enum.GetNames<SealSituation>());
        keys[1] = ConfigurationKey.Optional(DestinationKey);
        return keys;
    }

    private static T[] Only<T>(T item)
    {
        var items = new T[1];
        items[0] = item;
        return items;
    }

    private static string? ManualReason(string? destination) =>
        destination is null ? null : "o selo manual não tem destino: tire a chave, ou declare External ou Internal";

    private static string? DestinationReason(string? destination, string? databaseHost) =>
        destination is null ? "External e Internal exigem um destino https" : SealDestination.Refusal(destination, databaseHost);

    /// <summary>A destination missing is told as missing; one that breaks a rule is told with the rule and the address, except when the address carries a user, which is not shown.</summary>
    private static ConfigurationProblem Refused(string? destination, string reason) =>
        destination is null
            ? new ConfigurationProblem(ConfigurationProblemKind.MissingKey, DestinationPath, Found: null, Accepted: null)
            : new ConfigurationProblem(ConfigurationProblemKind.ValueNotAccepted, DestinationPath, Shown(destination), Only(reason));

    private static string? Shown(string destination) =>
        Uri.TryCreate(destination, UriKind.Absolute, out var address) && address.UserInfo.Length > 0 ? null : destination;
}

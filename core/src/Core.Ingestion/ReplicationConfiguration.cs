using System.Globalization;
using Core.Platform.Configuration;

namespace Core.Ingestion;

/// <summary>
/// The two values of the criterion of the replicated content in the configuration of the domain (decision 0024): each has a default and is shown with its origin in the effective configuration,
/// so that whoever reads the marks of a domain knows which values made them.
/// </summary>
public static class ReplicationConfiguration
{
    /// <summary>The key of the smallest length of a replicated text.</summary>
    public static string MinLengthPath { get; } = SectionName + ":" + MinLengthKey;

    /// <summary>The key of the smallest number of distinct senders of a replicated text.</summary>
    public static string MinSendersPath { get; } = SectionName + ":" + MinSendersKey;

    /// <summary>What the smallest length accepts.</summary>
    public static string MinLengthAccepted { get; } = "um número inteiro de pontos de código, de 1 até 100000";

    /// <summary>What the smallest number of senders accepts.</summary>
    public static string MinSendersAccepted { get; } = "um número inteiro de remetentes, de 2 até 1000";

    private const string SectionName = "Replication";
    private const string MinLengthKey = "MinLength";
    private const string MinSendersKey = "MinSenders";
    private const string DefaultMinLength = "20";
    private const string DefaultMinSenders = "2";
    private const int MostLength = 100_000;
    private const int MostSenders = 1_000;

    /// <summary>The section of the criterion: both keys have a default, so a domain that says nothing is judged by the decision's own numbers.</summary>
    public static ConfigurationSection Section { get; } =
        new(SectionName, Listed(ConfigurationKey.WithDefault(MinLengthKey, DefaultMinLength), ConfigurationKey.WithDefault(MinSendersKey, DefaultMinSenders)));

    /// <summary>What the values refuse: a number outside what the criterion makes sense with.</summary>
    /// <param name="values">The effective values.</param>
    public static IReadOnlyList<ConfigurationProblem> Refusals(IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var problems = new List<ConfigurationProblem>();
        if (values.TryGetValue(MinLengthPath, out var length) && Number(length, 1, MostLength) is null)
        {
            problems.Add(new ConfigurationProblem(ConfigurationProblemKind.ValueNotAccepted, MinLengthPath, length, Listed(MinLengthAccepted)));
        }

        if (values.TryGetValue(MinSendersPath, out var senders) && Number(senders, ReplicationRule.FewestSenders, MostSenders) is null)
        {
            problems.Add(new ConfigurationProblem(ConfigurationProblemKind.ValueNotAccepted, MinSendersPath, senders, Listed(MinSendersAccepted)));
        }

        return problems;
    }

    /// <summary>The criterion the values declare, which are checked before.</summary>
    /// <param name="values">The effective values.</param>
    public static ReplicationRule Rule(IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new ReplicationRule(Number(values[MinLengthPath], 1, MostLength)!.Value, Number(values[MinSendersPath], ReplicationRule.FewestSenders, MostSenders)!.Value);
    }

    private static T[] Listed<T>(params T[] items) => items;

    private static int? Number(string declared, int least, int most) =>
        int.TryParse(declared, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number >= least && number <= most ? number : null;
}

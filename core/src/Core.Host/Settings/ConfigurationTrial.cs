using Core.Platform.Configuration;

namespace Core.Host.Settings;

/// <summary>The judgment of a change against the schema and the rules of the start, which the desk applies on the submission and the applier applies again before it writes.</summary>
internal static class ConfigurationTrial
{
    /// <summary>What the start would refuse in the configuration that results from the values in force with the key changed; empty when the start would accept it.</summary>
    internal static IReadOnlyList<ConfigurationProblem> Problems(IReadOnlyDictionary<string, string> inForce, string key, string value) =>
        HostRefusals.OfService(ClosedConfiguration.Load(HostConfiguration.Schema(migrating: false, comparing: false), new TrialSource(inForce, key, value)));
}

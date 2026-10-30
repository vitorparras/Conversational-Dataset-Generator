using System.Text.Json.Serialization;

namespace Core.Platform;

/// <summary>What the installation declares itself to be, shown by the name the configuration takes.</summary>
public enum InstallationKind
{
    /// <summary>Holds real material, so it has a safekeeping copy of the key and a recovery that was tested.</summary>
    [JsonStringEnumMemberName("real")]
    Real = 0,

    /// <summary>Runs without a safekeeping copy and without a tested recovery, so it takes no real material.</summary>
    [JsonStringEnumMemberName("demonstracao")]
    Demonstration = 1,
}

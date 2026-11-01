namespace Core.Tests.Integration.Hosts;

/// <summary>
/// How a test wants the built host started: the arguments it adds to the ones every test needs (an added key replaces the default of the same key), the keys
/// it takes out, the variables of its environment, the settings file beside a copy of the binary, the real language service and the log file.
/// </summary>
internal sealed record HostSetup
{
    /// <summary>Arguments in the form <c>--Key=value</c>, added to the defaults.</summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>Keys of the defaults the host is started without.</summary>
    public IReadOnlyList<string> Omit { get; init; } = [];

    /// <summary>Variables the process starts with besides the ones it inherits.</summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();

    /// <summary>When given, the host runs from a copy of its binary directory that holds this settings file.</summary>
    public string? SettingsJson { get; init; }

    /// <summary>Whether the host asks the real language service, which it starts, instead of the fake recognizer.</summary>
    public bool RealService { get; init; }

    /// <summary>Whether the host also writes its log to a file of its own, which the scenario reads back.</summary>
    public bool WithLogFile { get; init; }

    /// <summary>The work factor the person's password is made with, which is the one the host is told when no argument says otherwise.</summary>
    public int WorkFactor { get; init; } = 1;
}

namespace Core.Platform.Sessions;

/// <summary>
/// The short description of the device a session was opened from, such as "Firefox no Windows", made from the <c>User-Agent</c> header read at the entry and from nothing else:
/// a browser and a system out of fixed lists, no version, no network address and never the raw text. An agent that is empty, absent, longer than <see cref="MaxAgentLength"/>
/// or not recognized says so, or gives only the part that was recognized.
/// </summary>
public static class SessionDevice
{
    /// <summary>What the description says when the agent tells nothing about the browser.</summary>
    public static string Unknown { get; } = "Navegador não identificado";

    /// <summary>The longest agent that is read; a longer one is not looked at.</summary>
    public static int MaxAgentLength { get; } = 512;

    // Order matters: Edge and Opera also say Chrome, and Chrome also says Safari.
    private static readonly (string Token, string Name)[] Browsers =
    [
        ("Edg", "Edge"), ("OPR", "Opera"), ("Opera", "Opera"), ("Firefox", "Firefox"), ("Chrome", "Chrome"), ("CriOS", "Chrome"), ("Safari", "Safari"),
    ];

    // Order matters: Android also says Linux, and iPhone and iPad also say Mac OS X.
    private static readonly (string Token, string Name)[] Systems =
    [
        ("Windows", "Windows"), ("Android", "Android"), ("iPhone", "iOS"), ("iPad", "iOS"), ("CrOS", "ChromeOS"), ("Mac OS X", "macOS"), ("Macintosh", "macOS"), ("Linux", "Linux"),
    ];

    /// <summary>The description of the agent; <see cref="Unknown"/> when neither a browser nor a system is recognized.</summary>
    public static string Describe(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent) || userAgent.Length > MaxAgentLength)
        {
            return Unknown;
        }

        var browser = NameOf(Browsers, userAgent);
        var system = NameOf(Systems, userAgent);
        return (browser, system) switch
        {
            (null, null) => Unknown,
            (null, _) => $"{Unknown} no {system}",
            (_, null) => browser,
            _ => $"{browser} no {system}",
        };
    }

    private static string? NameOf((string Token, string Name)[] known, string agent)
    {
        foreach (var (token, name) in known)
        {
            if (agent.Contains(token, StringComparison.Ordinal))
            {
                return name;
            }
        }

        return null;
    }
}

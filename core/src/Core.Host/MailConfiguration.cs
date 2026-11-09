using System.Globalization;
using Core.Platform.Configuration;
using ConfigurationSection = Core.Platform.Configuration.ConfigurationSection;

namespace Core.Host;

/// <summary>
/// The server of e-mail, which is optional (#635, requirements 022 and 032): with no key of the group, the system serves whole and the link that resets a password is handed over
/// outside it; with any key, the group must be whole and the transport ciphered (decision 0029), or the start is refused, naming what is missing or the value refused.
/// The keys are <c>Mail:Host</c>, <c>Mail:Port</c>, <c>Mail:Sender</c>, <c>Mail:Transport</c> (<c>Tls</c> or <c>StartTls</c>, nothing in clear), and the pair
/// <c>Mail:User</c> and <c>Mail:Password</c>, both or neither; the password is a secret, shown by its name and origin alone.
/// </summary>
internal static class MailConfiguration
{
    internal const string HostPath = SectionName + ":" + HostKey;

    internal const string PortPath = SectionName + ":" + PortKey;

    internal const string SenderPath = SectionName + ":" + SenderKey;

    internal const string TransportPath = SectionName + ":" + TransportKey;

    internal const string UserPath = SectionName + ":" + UserKey;

    internal const string PasswordPath = SectionName + ":" + PasswordKey;

    private const string SectionName = "Mail";
    private const string HostKey = "Host";
    private const string PortKey = "Port";
    private const string SenderKey = "Sender";
    private const string TransportKey = "Transport";
    private const string UserKey = "User";
    private const string PasswordKey = "Password";
    private const int HighestPort = 65_535;

    private static readonly string[] Whole = { HostPath, PortPath, SenderPath, TransportPath };

    private static readonly string[] Pair = { UserPath, PasswordPath };

    /// <summary>Every key is optional to the schema, which cannot say that a group is all or nothing; <see cref="Refusals"/> says it. The transport accepts no clear text.</summary>
    internal static ConfigurationSection Section { get; } = new(SectionName, Keys());

    /// <summary>What a group declared in part, or with a value it cannot take, breaks, naming every key at fault; nothing for a group not declared at all.</summary>
    internal static IReadOnlyList<ConfigurationProblem> Refusals(IReadOnlyDictionary<string, string> values)
    {
        var problems = new List<ConfigurationProblem>();
        if (!Declared(values))
        {
            return problems;
        }

        problems.AddRange(Whole.Where(path => !values.ContainsKey(path)).Select(Missing));
        if (Pair.Any(values.ContainsKey))
        {
            problems.AddRange(Pair.Where(path => !values.ContainsKey(path)).Select(Missing));
        }

        if (values.TryGetValue(PortPath, out var port) && !IsPort(port))
        {
            problems.Add(new ConfigurationProblem(ConfigurationProblemKind.ValueNotAccepted, PortPath, port, Only($"um número de porta, de 1 a {HighestPort}")));
        }

        return problems;
    }

    /// <summary>The server the configuration declared, once judged whole; none when the group is absent, and the reset of a password is then handed over by hand.</summary>
    internal static MailServer? Declaration(IReadOnlyDictionary<string, string> values) =>
        Declared(values)
            ? new MailServer(values[HostPath], int.Parse(values[PortPath], CultureInfo.InvariantCulture), values[SenderPath], Enum.Parse<MailTransport>(values[TransportPath]), values.GetValueOrDefault(UserPath))
            : null;

    private static bool Declared(IReadOnlyDictionary<string, string> values) => Whole.Concat(Pair).Any(values.ContainsKey);

    private static bool IsPort(string text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port is >= 1 and <= HighestPort;

    private static ConfigurationProblem Missing(string path) => new(ConfigurationProblemKind.MissingKey, path, Found: null, Accepted: null);

    private static string[] Only(string rule)
    {
        var rules = new string[1];
        rules[0] = rule;
        return rules;
    }

    private static ConfigurationKey[] Keys()
    {
        var keys = new ConfigurationKey[6];
        keys[0] = ConfigurationKey.Optional(HostKey);
        keys[1] = ConfigurationKey.Optional(PortKey);
        keys[2] = ConfigurationKey.Optional(SenderKey);
        keys[3] = ConfigurationKey.Optional(TransportKey).Accepting(nameof(MailTransport.Tls), nameof(MailTransport.StartTls));
        keys[4] = ConfigurationKey.Optional(UserKey);
        keys[5] = ConfigurationKey.Optional(PasswordKey).AsSensitive();
        return keys;
    }
}

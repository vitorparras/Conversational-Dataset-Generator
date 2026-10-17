namespace Core.Platform.Configuration.Sources;

/// <summary>Reads arguments of the exact form <c>--Section:Key=value</c>; anything else is a problem, never an exception.</summary>
public sealed class StrictArgumentsSource : IConfigurationSource
{
    private const string Marker = "--";
    private const char Assignment = '=';

    private readonly IReadOnlyList<string> arguments;

    /// <summary>The source over the arguments given, in the order given.</summary>
    public StrictArgumentsSource(IEnumerable<string> arguments)
    {
        this.arguments = [.. arguments];
    }

    /// <inheritdoc />
    public SourceReading Read() =>
        new(
            ConfigurationOrigin.Argument,
            [.. arguments.Where(IsWellFormed).Select(Parse)],
            [.. arguments.Where(argument => !IsWellFormed(argument)).Select(Malformed)]);

    /// <summary>The marker, a non-empty key and the assignment sign, in that order.</summary>
    private static bool IsWellFormed(string argument) =>
        argument.StartsWith(Marker, StringComparison.Ordinal)
        && argument.IndexOf(Assignment, StringComparison.Ordinal) > Marker.Length;

    private static KeyValuePair<string, string?> Parse(string argument)
    {
        var assignment = argument.IndexOf(Assignment, StringComparison.Ordinal);
        return KeyValuePair.Create(argument[Marker.Length..assignment], (string?)argument[(assignment + 1)..]);
    }

    /// <summary>Names the argument up to the assignment sign, so that whatever value it carried is never echoed.</summary>
    private static ConfigurationProblem Malformed(string argument)
    {
        var assignment = argument.IndexOf(Assignment, StringComparison.Ordinal);
        var shown = assignment < 0 ? argument : argument[..assignment];
        return new ConfigurationProblem(ConfigurationProblemKind.MalformedArgument, shown, Found: null, Accepted: null);
    }
}

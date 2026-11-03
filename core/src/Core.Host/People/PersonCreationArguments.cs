namespace Core.Host.People;

/// <summary>What <c>create-person</c> takes from its arguments: the name and the function, once each, and the rest for the configuration; the password is never among them.</summary>
internal sealed class PersonCreationArguments
{
    private const string NameOption = "--name";
    private const string FunctionOption = "--function";
    private const string PasswordOption = "--password";

    /// <summary>The name given; null when it was not given exactly once.</summary>
    internal string? Name { get; private init; }

    /// <summary>The function given; null when it was not given exactly once.</summary>
    internal string? Function { get; private init; }

    /// <summary>Whether a password was offered as an argument, which is refused whatever it holds and is never repeated.</summary>
    internal bool PasswordOffered { get; private init; }

    /// <summary>The arguments that are none of these, for the configuration.</summary>
    internal required string[] Remaining { get; init; }

    /// <summary>Takes the options out of the arguments that follow the mode word; each is written <c>--option value</c> or <c>--option=value</c>.</summary>
    internal static PersonCreationArguments Split(string[] args)
    {
        var (names, afterName) = Take(args.Skip(1), NameOption);
        var (functions, afterFunction) = Take(afterName, FunctionOption);
        var (passwords, rest) = Take(afterFunction, PasswordOption);
        var remaining = new string[rest.Count];
        rest.CopyTo(remaining);
        return new PersonCreationArguments
        {
            Name = names.Count == 1 ? names[0] : null,
            Function = functions.Count == 1 ? functions[0] : null,
            PasswordOffered = passwords.Count > 0,
            Remaining = remaining,
        };
    }

    private static (List<string> Values, List<string> Others) Take(IEnumerable<string> args, string option)
    {
        var values = new List<string>();
        var rest = new List<string>();
        var pending = new Queue<string>(args);
        while (pending.TryDequeue(out var current))
        {
            if (current == option)
            {
                values.Add(pending.TryDequeue(out var value) ? value : string.Empty);
            }
            else if (current.StartsWith(option + "=", StringComparison.Ordinal))
            {
                values.Add(current[(option.Length + 1)..]);
            }
            else
            {
                rest.Add(current);
            }
        }

        return (values, rest);
    }
}

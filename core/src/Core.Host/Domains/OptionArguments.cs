namespace Core.Host.Domains;

/// <summary>What a one-shot mode takes from the arguments that follow its word: one option, written <c>--option value</c> or <c>--option=value</c>, and the rest for the configuration.</summary>
internal static class OptionArguments
{
    /// <summary>The value of the option given exactly once and not empty, or null; and the arguments left, the mode word out.</summary>
    internal static (string? Value, string[] Remaining) Once(string[] args, string option)
    {
        var prefix = option + "=";
        var rest = new List<string>();
        var values = new List<string>();
        var pending = new Queue<string>(args.Skip(1));
        while (pending.TryDequeue(out var current))
        {
            if (current == option)
            {
                values.Add(pending.TryDequeue(out var value) ? value : string.Empty);
            }
            else if (current.StartsWith(prefix, StringComparison.Ordinal))
            {
                values.Add(current[prefix.Length..]);
            }
            else
            {
                rest.Add(current);
            }
        }

        var remaining = new string[rest.Count];
        rest.CopyTo(remaining);
        return (values.Count == 1 && values[0].Length > 0 ? values[0] : null, remaining);
    }
}

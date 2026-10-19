namespace Core.Tests.Integration.Fixtures;

/// <summary>Reads the entries of one block of a compose file, enough for the few keys the tests look at, without a YAML library.</summary>
internal static class ComposeEntries
{
    /// <summary>The lines nested under the only <c>key:</c> line, trimmed, without list dashes or quotes.</summary>
    public static IReadOnlyList<string> Under(IReadOnlyList<string> lines, string key)
    {
        var start = lines.Select((line, index) => new NumberedLine(index, line)).Single(entry => entry.Text.Trim() == key + ":");
        var indent = Indent(start.Text);
        return
        [
            .. lines.Skip(start.Index + 1)
                .TakeWhile(line => line.Trim().Length == 0 || Indent(line) > indent)
                .Where(line => line.Trim().Length > 0)
                .Select(line => line.Trim().TrimStart('-').Trim().Trim('"')),
        ];
    }

    private static int Indent(string line) => line.Length - line.TrimStart().Length;

    private sealed record NumberedLine(int Index, string Text);
}

using System.Text.RegularExpressions;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>The rules the root config switches off with a reason, each with the section it sits in.</summary>
internal sealed partial class RootExclusions
{
    private const string ReasonMark = "#";
    private const string AllSources = "*.cs";

    /// <summary>The only rules that may be switched off for every source; any other is switched off for one named file.</summary>
    private static readonly string[] ProjectWide = ["S1309", "S1451", "S4055"];

    private readonly List<(string Header, string Id)> entries;
    private readonly string directory;

    private RootExclusions(List<(string Header, string Id)> entries, string directory)
    {
        this.entries = entries;
        this.directory = directory;
    }

    /// <summary>A rule counts as switched off when one comment line of real words stands directly above its <c>none</c>.</summary>
    public static RootExclusions Read(RootConfig root)
    {
        var lines = new[] { string.Empty, string.Empty }.Concat(root.Text.ReplaceLineEndings("\n").Split('\n')).ToList();
        var header = string.Empty;
        var entries = new List<(string Header, string Id)>();
        foreach (var (above, comment, line) in lines.Zip(lines.Skip(1), lines.Skip(2)))
        {
            header = Section().Match(line) is { Success: true } section ? section.Groups["name"].Value : header;
            if (Switch().Match(line) is { Success: true } match && IsReason(above, comment))
            {
                entries.Add((header, match.Groups["id"].Value));
            }
        }

        return new RootExclusions(entries, root.Directory);
    }

    public bool Covers(string id, string source) =>
        entries.Exists(entry => entry.Id == id && (IsTheFile(entry.Header, source) || IsWideAndAllowed(entry.Header, id)));

    /// <summary>A single comment line of real words: a comment block above it is a header, not a reason.</summary>
    private static bool IsReason(string above, string comment) =>
        Reason().IsMatch(comment) && !above.TrimStart().StartsWith(ReasonMark, StringComparison.Ordinal);

    private static bool IsWideAndAllowed(string header, string id) => header == AllSources && Array.Exists(ProjectWide, wide => wide == id);

    private bool IsTheFile(string header, string source) =>
        string.Equals(Path.GetFullPath(Path.Combine(directory, header)), Path.GetFullPath(source), StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"^dotnet_diagnostic\.(?<id>S\d+)\.severity = none$")]
    private static partial Regex Switch();

    [GeneratedRegex(@"^\[(?<name>.+)\]$")]
    private static partial Regex Section();

    [GeneratedRegex(@"^#(\s+\S+){3,}\s*$")]
    private static partial Regex Reason();
}

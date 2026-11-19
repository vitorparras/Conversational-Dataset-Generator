using System.Text;
using Core.Boundary.Contracts;

namespace Core.Ingestion.Sanitizing;

/// <summary>
/// Takes away what the form of origin added and nobody wrote: the line that opens or closes every page of a converted document, its numbers aside, with its line break. It works
/// by a pattern repeated on every page of a document of three pages or more, split by the page break, and is the most conservative of the steps, since a greedy pattern eats
/// written text and leaves no gap (requirement 005): a written line with the same pattern stays and is counted, the marker a tool put in place of missing content stays, and no
/// page is left without a written line. In doubt, the artifact survives.
/// </summary>
public sealed class FormatArtifactStep : ISanitizationStep
{
    private const int MinimumPages = 3;
    private const string PageBreak = "\f";
    private const string LineBreak = "\n";

    /// <summary>The words a tool writes in place of a message that was taken out before the export.</summary>
    private static readonly string[] AbsentContent = ["Esta mensagem foi apagada", "Mensagem apagada"];

    private readonly ITextBoundaries boundaries;

    /// <summary>The removal, which walks the text by the code points the boundary gives.</summary>
    /// <param name="boundaries">Splits the text into its code points and counts what the removal writes.</param>
    public FormatArtifactStep(ITextBoundaries boundaries)
    {
        ArgumentNullException.ThrowIfNull(boundaries);
        this.boundaries = boundaries;
    }

    /// <inheritdoc />
    public string Name => "artefatos";

    /// <inheritdoc />
    public int Version => 1;

    /// <inheritdoc />
    public StepScope? Scope { get; } = new(
        "retira, de documento de três páginas ou mais separadas por quebra de página, a linha que abre ou fecha toda página com o mesmo padrão, números à parte, com a quebra de linha dela",
        "toda outra linha, a linha escrita que coincide com o padrão retirado, o marcador que a ferramenta pôs no lugar de conteúdo ausente e a página que ficaria sem linha escrita");

    /// <inheritdoc />
    public IReadOnlyList<SanitizationEdit> Edits(StepInput input) =>
        [.. Found(input).Removed.Select(line => SanitizationEdit.Rewriting(line.From, line.Next, string.Empty, boundaries))];

    /// <inheritdoc />
    public int Untreated(StepInput input) => Found(input).Coincidences;

    private (List<Line> Removed, int Coincidences) Found(StepInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var pages = Pages(boundaries.CodePoints(input.Text));
        if (pages.Count < MinimumPages)
        {
            return ([], 0);
        }

        var header = Depth(pages, 0, (page, depth) => page.Lines[depth]);
        var footer = Depth(pages, header, (page, depth) => page.Lines[^(depth + 1)]);
        var removed = pages.SelectMany(page => page.Lines.Take(header).Concat(page.Lines.TakeLast(footer))).OrderBy(line => line.From).ToList();
        var patterns = removed.Select(line => line.Pattern).ToHashSet(StringComparer.Ordinal);
        var coincidences = pages.SelectMany(page => page.Lines.Skip(header).SkipLast(footer)).Count(line => patterns.Contains(line.Pattern));
        return (removed, coincidences);
    }

    /// <summary>How many lines, from the edge given, every page opens or closes with under one pattern, each page keeping a written line besides those already taken.</summary>
    private static int Depth(List<Page> pages, int taken, Func<Page, int, Line> edge)
    {
        var depth = 0;
        while (pages.TrueForAll(page => page.Lines.Count > taken + depth + 1) && Shared(pages.Select(page => edge(page, depth))))
        {
            depth++;
        }

        return depth;
    }

    /// <summary>Whether the lines have one pattern and none of them stands for missing content.</summary>
    private static bool Shared(IEnumerable<Line> lines)
    {
        var patterns = lines.Select(line => line.Absent ? null : line.Pattern).Distinct().ToList();
        return patterns is [{ }];
    }

    /// <summary>The written lines of each page, those with a character besides spaces, in order.</summary>
    private static List<Page> Pages(IEnumerable<string> points)
    {
        var pages = new List<Page> { new() };
        var line = new StringBuilder();
        var from = 0;
        var at = 0;
        foreach (var point in points.Append(PageBreak))
        {
            if (point is PageBreak or LineBreak)
            {
                AddWritten(pages[^1], line.ToString(), from, point == LineBreak ? at + 1 : at);
                pages.AddRange(point == PageBreak ? [new Page()] : []);
                (line, from) = (new StringBuilder(), at + 1);
            }
            else
            {
                _ = line.Append(point);
            }

            at++;
        }

        pages.RemoveAt(pages.Count - 1);
        return pages;
    }

    private static void AddWritten(Page page, string text, int from, int next)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            page.Lines.Add(new Line(from, next, PatternOf(text), IsAbsentContent(text.Trim())));
        }
    }

    /// <summary>The line with every run of digits as one sign, so that numbered pages share a pattern.</summary>
    private static string PatternOf(string text)
    {
        var pattern = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            var digit = char.IsAsciiDigit(character);
            if (!digit || pattern.Length == 0 || pattern[^1] != '#')
            {
                _ = pattern.Append(digit ? '#' : character);
            }
        }

        return pattern.ToString();
    }

    private static bool IsAbsentContent(string trimmed) =>
        (trimmed.StartsWith('<') && trimmed.EndsWith('>')) || AbsentContent.Contains(trimmed, StringComparer.OrdinalIgnoreCase);

    /// <summary>The written lines of one page, in order.</summary>
    private sealed class Page
    {
        public List<Line> Lines { get; } = [];
    }

    /// <summary>A written line: where it starts, where the next one starts, in code points, its pattern and whether it stands for missing content.</summary>
    private sealed record Line(int From, int Next, string Pattern, bool Absent);
}

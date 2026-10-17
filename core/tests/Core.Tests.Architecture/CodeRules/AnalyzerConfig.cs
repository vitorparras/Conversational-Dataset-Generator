namespace Core.Tests.Architecture.CodeRules;

/// <summary>One <c>.editorconfig</c> or <c>.globalconfig</c> as the rules see it: its path and its text.</summary>
internal sealed record AnalyzerConfig(string Path, string Text)
{
    public static AnalyzerConfig Read(string path) => new(path, File.ReadAllText(path));

    /// <summary>Every line with its number, as an editor counts them.</summary>
    public IEnumerable<ConfigLine> Lines() =>
        Text.Split('\n').Select((text, index) => new ConfigLine(index + 1, text.TrimEnd('\r')));
}

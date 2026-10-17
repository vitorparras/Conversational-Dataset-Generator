using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>A file on disk handed to the generators as an additional text.</summary>
internal sealed class FileText : AdditionalText
{
    public FileText(string path)
    {
        Path = path;
    }

    public override string Path { get; }

    public override SourceText? GetText(CancellationToken cancellationToken = default) =>
        SourceText.From(File.ReadAllText(Path), Encoding.UTF8);
}

using System.Xml.Linq;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>One project file as the rules see it: the module it names and its XML.</summary>
internal sealed class ProjectFile
{
    private ProjectFile(string path, string text)
    {
        Path = path;
        Document = XDocument.Parse(text);
    }

    public string Path { get; }

    public XDocument Document { get; }

    public string ModuleName => System.IO.Path.GetFileNameWithoutExtension(Path);

    public static ProjectFile Read(string path) => new(path, File.ReadAllText(path));

    public static ProjectFile FromText(string path, string text) => new(path, text);

    public IEnumerable<string> InternalsVisibleTo() =>
        Document.Descendants("InternalsVisibleTo")
            .Select(element => element.Attribute("Include")?.Value)
            .OfType<string>();
}

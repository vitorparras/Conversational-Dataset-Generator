using System.Xml;
using System.Xml.Linq;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>One project file as the rules see it: the module it names, its kind and its XML.</summary>
internal sealed class ProjectFile
{
    private ProjectFile(string path, string text)
    {
        Path = path;
        Document = XDocument.Parse(text, LoadOptions.SetLineInfo);
    }

    public string Path { get; }

    public XDocument Document { get; }

    public string ModuleName => System.IO.Path.GetFileNameWithoutExtension(Path);

    public string DirectoryPath => System.IO.Path.GetDirectoryName(Path) ?? string.Empty;

    public bool IsExecutable => Document.Descendants("OutputType").Any(element => element.Value.Trim() == "Exe");

    public static ProjectFile Read(string path) => new(path, File.ReadAllText(path));

    public static ProjectFile FromText(string path, string text) => new(path, text);

    public IEnumerable<InternalsGrant> InternalsVisibleTo() =>
        Document.Descendants("InternalsVisibleTo")
            .Select(element => (Element: element, Target: element.Attribute("Include")?.Value))
            .Where(entry => entry.Target is not null)
            .Select(entry => new InternalsGrant(entry.Target!, ((IXmlLineInfo)entry.Element).LineNumber));
}

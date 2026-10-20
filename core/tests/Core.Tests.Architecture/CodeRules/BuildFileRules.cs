using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>
/// The build files of the repository read nothing that tells the build of a guard from the build the gate runs, and take no asset away
/// from a package they reference: a file that changes what it does on such a signal lets the guard pass while the gate is silenced.
/// </summary>
internal static partial class BuildFileRules
{
    private const string SignalAdvice =
        "Um arquivo de build que muda de comportamento por esse sinal age diferente na guarda e no gate; tire a referência ou faça a mudança valer igual nos dois builds";

    private const string AssetAdvice = "filtrar os ativos de um pacote pode tirar dele o analisador ou os alvos de build; declare a referência sem";

    private static readonly string[] Extensions = [".props", ".targets", ".csproj", ".proj", ".tasks"];
    private static readonly string[] SkippedDirectories = [".git", "bin", "obj", "node_modules", ".venv", ".verify-code"];
    private static readonly string[] ReferenceElements = ["PackageReference", "PackageVersion", "GlobalPackageReference"];
    private static readonly string[] AssetFilters = ["ExcludeAssets", "IncludeAssets"];

    /// <summary>Each signal, with what it tells apart; matched as a whole word in any letter case, the way MSBuild reads a property.</summary>
    private static readonly (Regex Signal, string Reason)[] Signals =
    [
        (GuardProperties(), "só o build de uma guarda define essa propriedade (--artifacts-path, alvos acrescentados, lock redirecionado, compilação sem servidor)"),
        (SolutionProperties(), "só o build pela solução define essa propriedade, e a guarda constrói o projeto sozinho"),
        (RunnerEnvironment(), "essa variável vem do ambiente de quem executa os testes, não do build do gate"),
        (TemporaryPlaces(), "isso nomeia um caminho temporário ou de canário, onde só a guarda constrói"),
    ];

    /// <summary>Every build file under the root, skipping version control and build output.</summary>
    public static IEnumerable<Violation> Check(string repositoryRoot) =>
        BuildFiles(repositoryRoot).SelectMany(CheckFile);

    public static IEnumerable<Violation> CheckFile(string path)
    {
        var document = XDocument.Load(path, LoadOptions.SetLineInfo);
        return document.Descendants().SelectMany(element => CheckSignals(path, element).Concat(CheckAssets(path, element)));
    }

    private static IEnumerable<Violation> CheckSignals(string path, XElement element) =>
        element.Attributes().Select(attribute => (Text: attribute.Value, Place: $"o atributo {attribute.Name.LocalName} de {element.Name.LocalName}"))
            .Concat(element.Nodes().OfType<XText>().Select(text => (Text: text.Value, Place: $"o valor de {element.Name.LocalName}")))
            .SelectMany(entry => Signals.SelectMany(signal => signal.Signal.Matches(entry.Text).Select(match =>
                new Violation(path, LineOf(element), $"{entry.Place} lê {match.Value}: {signal.Reason}. {SignalAdvice}"))));

    private static IEnumerable<Violation> CheckAssets(string path, XElement element) =>
        ReferenceElements.Contains(element.Name.LocalName, StringComparer.Ordinal)
            ? AssetFilters
                .Where(filter => element.Attribute(filter) is not null || element.Element(element.Name.Namespace + filter) is not null)
                .Select(filter => new Violation(
                    path,
                    LineOf(element),
                    $"{element.Name.LocalName} de {PackageOf(element)} declara {filter}: {AssetAdvice} {filter}"))
            : [];

    private static IEnumerable<string> BuildFiles(string directory) =>
        Directory.EnumerateFiles(directory)
            .Where(file => Extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            .Concat(Directory.EnumerateDirectories(directory)
                .Where(child => !SkippedDirectories.Contains(Path.GetFileName(child), StringComparer.OrdinalIgnoreCase))
                .SelectMany(BuildFiles));

    private static string PackageOf(XElement element) =>
        (element.Attribute("Include") ?? element.Attribute("Update"))?.Value ?? string.Empty;

    private static int LineOf(XElement element) => ((IXmlLineInfo)element).LineNumber;

    [GeneratedRegex(
        @"\b(ArtifactsPath|UseArtifactsOutput|ArtifactsPivots|Custom(After|Before)Microsoft(Common|CSharp)(Targets|Props)|NuGetLockFilePath|RestoreLockedMode"
            + @"|UseSharedCompilation|ProvideCommandLineArgs|MSBuildStartupDirectory)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex GuardProperties();

    [GeneratedRegex(@"\b(Solution(Dir|Name|Path|FileName|Ext)|CurrentSolutionConfigurationContents|BuildingSolutionFile)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SolutionProperties();

    [GeneratedRegex(@"\b((DOTNET|TESTINGPLATFORM|VSTEST)_\w*|NUGET_PACKAGES|GetEnvironmentVariables?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex RunnerEnvironment();

    [GeneratedRegex(@"\b(TEMP|TMP|TMPDIR|GetTempPath|GetTempFileName)\b|canary|scratch-", RegexOptions.IgnoreCase)]
    private static partial Regex TemporaryPlaces();
}

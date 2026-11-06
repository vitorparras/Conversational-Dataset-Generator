using System.Text.RegularExpressions;
using Core.Tests.Architecture.CodeRules;
using Microsoft.CodeAnalysis;

namespace Core.Tests.Architecture.Boundaries;

/// <summary>The host only composes, serves endpoints and supervises, so no name it declares, and no file it holds, speaks the product's domain.</summary>
internal static partial class NoDomainRuleInHostRules
{
    private const string ComponentExtension = ".razor";
    private const string RazorGenerator = "/Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator/";

    /// <summary>Stems of the forms of material, the parts of a conversation, the pipeline stages, the custody of the material, the taxonomy and the people in the material.</summary>
    private static readonly string[] DomainTerms =
    [
        "Conversation", "Comment", "Document", "Label", "Message", "Participant", "Person", "People",
        "Ingest", "Sanitiz", "Pseudonym", "Annotat", "Agreement", "Arbitrat", "Export", "Custod",
        "Taxonom", "Categor",
    ];

    /// <summary>Words after which <c>Message</c> names a log line, as in the <c>*LogMessages</c> classes or a <c>logMessage</c>, not a part of a conversation.</summary>
    private static readonly string[] LoggingWords = ["Log", "Logger"];

    private static readonly char[] PathSeparators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    private static readonly SymbolKind[] JudgedKinds =
        [SymbolKind.Namespace, SymbolKind.Alias, SymbolKind.NamedType, SymbolKind.TypeParameter, SymbolKind.Method, SymbolKind.Property, SymbolKind.Field, SymbolKind.Event, SymbolKind.Parameter];

    /// <summary>The sources the compiler is given for the host, wherever they sit.</summary>
    public static IReadOnlyList<SourceFile> HostFiles() =>
        CompilerInvocation.Of(SourceTree.ProductionProject(ModuleBoundaryRules.Host).Path).SourceFiles;

    /// <summary>The sources of the host, its Razor components, and what the Razor generator writes for them: the class, the members and the parameters a component declares.</summary>
    public static IReadOnlyList<SourceFile> HostAndComponentFiles()
    {
        var invocation = CompilerInvocation.Of(SourceTree.ProductionProject(ModuleBoundaryRules.Host).Path);
        return [.. invocation.SourceFiles, .. invocation.GeneratorFiles.Where(IsRazorOutput), .. HostComponents()];
    }

    /// <summary>Every name or file name that carries a domain term; a component is judged by its file name, and the names inside it by what the generator writes.</summary>
    public static IEnumerable<Violation> Check(IReadOnlyList<SourceFile> files) =>
        files.SelectMany(FileNameFindings).Concat(Declarations([.. files.Where(file => !IsComponent(file))]).SelectMany(NameFindings)).Distinct();

    /// <summary>Every name the files declare, as the compiler binds it: namespaces, aliases, types, type parameters, members and their parameters.</summary>
    public static IEnumerable<string> DeclaredNames(IReadOnlyList<SourceFile> files) =>
        Declarations(files).Select(declaration => declaration.Name);

    /// <summary>The components under the host project, wherever they sit in it; the build output is no component.</summary>
    private static IEnumerable<SourceFile> HostComponents() =>
        Directory.EnumerateFiles(Path.GetDirectoryName(SourceTree.ProductionProject(ModuleBoundaryRules.Host).Path)!, "*" + ComponentExtension, SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Order(StringComparer.Ordinal)
            .Select(SourceFile.Read);

    private static bool IsBuildOutput(string path) =>
        Array.Exists(path.Split(PathSeparators), part => part is "bin" or "obj");

    private static bool IsComponent(SourceFile file) =>
        string.Equals(Path.GetExtension(file.Path), ComponentExtension, StringComparison.Ordinal);

    private static bool IsRazorOutput(SourceFile file) =>
        file.Path.Replace('\\', '/').Contains(RazorGenerator, StringComparison.Ordinal);

    private static IEnumerable<Declaration> Declarations(IReadOnlyList<SourceFile> files)
    {
        var compilation = SourceCompilation.Of(files);
        return files.SelectMany(file => DeclarationsIn(file, compilation.GetSemanticModel(file.Tree)));
    }

    private static IEnumerable<Declaration> DeclarationsIn(SourceFile file, SemanticModel model) =>
        file.Root.DescendantNodes()
            .Select(node => (Node: node, Symbol: model.GetDeclaredSymbol(node)))
            .Where(entry => IsJudged(entry.Symbol))
            .Select(entry => new Declaration(file, entry.Node, entry.Symbol!));

    private static bool IsJudged(ISymbol? symbol) => symbol switch
    {
        null => false,
        IMethodSymbol method => method.MethodKind is MethodKind.Ordinary or MethodKind.ExplicitInterfaceImplementation,
        IParameterSymbol { ContainingSymbol: IMethodSymbol method } => method.MethodKind is MethodKind.Ordinary or MethodKind.Constructor,
        _ => JudgedKinds.Contains(symbol.Kind),
    };

    private static IEnumerable<Violation> NameFindings(Declaration declaration) =>
        TermsIn(declaration.Name).Select(term =>
            new Violation(declaration.File.Path, SourceFile.LineOf(declaration.Node), $"{declaration.Symbol.Kind} {declaration.Name} names the domain term {term}"));

    private static IEnumerable<Violation> FileNameFindings(SourceFile file) =>
        TermsIn(Path.GetFileNameWithoutExtension(file.FileName)).Select(term =>
            new Violation(file.Path, 1, $"file name {file.FileName} names the domain term {term}"));

    /// <summary>The domain terms that begin some word of a name, with the name split where its case changes.</summary>
    private static IEnumerable<string> TermsIn(string name)
    {
        var words = WordBoundary().Split(name).Where(word => word.Length > 0).ToList();
        return words.SelectMany((word, index) => DomainTerms
            .Where(term => word.StartsWith(term, StringComparison.OrdinalIgnoreCase))
            .Where(term => !NamesALogLine(term, words.ElementAtOrDefault(index - 1))));
    }

    private static bool NamesALogLine(string term, string? previousWord) =>
        term == "Message" && LoggingWords.Contains(previousWord, StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex(@"[_.]|(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])")]
    private static partial Regex WordBoundary();

    private sealed record Declaration(SourceFile File, SyntaxNode Node, ISymbol Symbol)
    {
        /// <summary>A namespace is judged whole, since only its last part is its own name.</summary>
        public string Name => Symbol is INamespaceSymbol ? Symbol.ToDisplayString() : Symbol.Name;
    }
}

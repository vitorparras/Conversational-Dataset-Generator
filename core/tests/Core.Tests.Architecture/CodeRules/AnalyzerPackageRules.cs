namespace Core.Tests.Architecture.CodeRules;

/// <summary>
/// Every analyzer a build loads comes from the SDK or from a package on a closed list, at the version and with the content hash
/// the project's lock file pins; the banned-symbol diagnostic comes only from the pinned analyzer package.
/// </summary>
internal static class AnalyzerPackageRules
{
    public const string BannedSymbolPackage = "Microsoft.CodeAnalysis.BannedApiAnalyzers";
    private const string OnlyByPackage = "analisador só entra por pacote da lista, restaurado pelo lock";
    private const string OnlyThePinned = "quem emite a regra do log é só o pacote fixado";
    private const string NewPackage = "pacote novo de analisador entra na lista deste teste, com o motivo";

    /// <summary>The packages allowed to bring an analyzer or a generator into a production build.</summary>
    private static readonly string[] AcceptedPackages =
    [
        BannedSymbolPackage,
        "SonarAnalyzer.CSharp",
        // Generators of the options validation and of the telemetry logging the platform uses.
        "Microsoft.Extensions.Options",
        "Microsoft.Extensions.Telemetry.Abstractions",
    ];

    /// <summary>Each analyzer is from the SDK, or from an accepted package whose version and hash are the ones the lock pins.</summary>
    public static IEnumerable<Violation> CheckPackages(CompilerInvocation invocation, string lockPath)
    {
        var pinned = PackageIdentity.InLock(lockPath);
        return invocation.Analyzers
            .Where(path => !IsUnder(path, ToolLocations.DotnetRoot))
            .SelectMany(path => invocation.AnalyzerPackages.TryGetValue(path, out var package)
                ? CheckPackage(Path.GetFileName(path), package, pinned, lockPath)
                : [$"o analisador {Path.GetFileName(path)} vem de fora do SDK e do cache de pacotes; {OnlyByPackage}"])
            .Select(message => new Violation(invocation.ProjectPath, 1, message));
    }

    /// <summary>The analyzers able to report the banned-symbol diagnostic are all, and only, the pinned package at its pinned version and hash.</summary>
    public static IEnumerable<Violation> CheckBannedSymbolEmitter(CompilerInvocation invocation, string lockPath)
    {
        var pinned = PackageIdentity.InLock(lockPath);
        var emitters = invocation.DiagnosticsByAnalyzer
            .Where(entry => entry.Value.Contains(LoggingSuppressionRules.Diagnostic, StringComparer.Ordinal))
            .Select(entry => entry.Key)
            .ToList();
        if (emitters.Count == 0)
        {
            return [new Violation(invocation.ProjectPath, 1, $"nenhum analisador carregado emite {LoggingSuppressionRules.Diagnostic}: o pacote {BannedSymbolPackage} saiu do build")];
        }

        return emitters
            .Where(path => !IsPinnedBannedSymbolPackage(invocation.AnalyzerPackages.GetValueOrDefault(path), pinned))
            .Select(path => new Violation(
                invocation.ProjectPath,
                1,
                $"{LoggingSuppressionRules.Diagnostic} é emitido por {Path.GetFileName(path)}, que não é o pacote {BannedSymbolPackage} na versão e com o hash que o lock fixa; {OnlyThePinned}"));
    }

    private static IEnumerable<string> CheckPackage(string analyzer, PackageIdentity package, IReadOnlyDictionary<string, PackageIdentity> pinned, string lockPath)
    {
        if (!AcceptedPackages.Contains(package.Id, StringComparer.OrdinalIgnoreCase))
        {
            yield return $"o analisador {analyzer} vem do pacote {package.Id}, que não está na lista de pacotes de analisador aceitos; {NewPackage}";
        }

        if (!pinned.TryGetValue(package.Id, out var locked))
        {
            yield return $"o pacote {package.Id} do analisador {analyzer} não está em {Path.GetFileName(lockPath)}: entrou por fora da restauração travada";
            yield break;
        }

        if (!string.Equals(locked.Version, package.Version, StringComparison.OrdinalIgnoreCase))
        {
            yield return $"o analisador {analyzer} vem de {package.Id} {package.Version}, e o lock fixa a versão {locked.Version}";
        }

        if (locked.ContentHash != package.ContentHash)
        {
            yield return $"o pacote {package.Id} {package.Version} no cache não é o que o lock fixa: o hash do conteúdo difere; restaure em modo travado num cache limpo";
        }
    }

    private static bool IsPinnedBannedSymbolPackage(PackageIdentity? package, IReadOnlyDictionary<string, PackageIdentity> pinned) =>
        package is not null
        && string.Equals(package.Id, BannedSymbolPackage, StringComparison.OrdinalIgnoreCase)
        && pinned.TryGetValue(BannedSymbolPackage, out var locked)
        && IsSameContent(locked, package);

    private static bool IsSameContent(PackageIdentity locked, PackageIdentity cached) =>
        string.Equals(locked.Version, cached.Version, StringComparison.OrdinalIgnoreCase) && locked.ContentHash == cached.ContentHash;

    private static bool IsUnder(string path, string root) =>
        Path.GetFullPath(path).StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}

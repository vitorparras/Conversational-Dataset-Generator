namespace Core.Tests.Architecture.CodeRules;

/// <summary>
/// The build the guards see and a build the build files cannot tell from the gate's must give the compiler the same inputs, and the
/// latter must refuse every banned route: a difference means a build file behaves one way under the guard and another under the gate.
/// </summary>
internal static class UnseenBuildRules
{
    private const string Hint =
        "algum arquivo de build se comporta diferente quando percebe a guarda; procure condição, alvo ou função de propriedade que dependa de como o build é chamado "
        + "(--artifacts-path, propriedade na linha de comando, solução, caminho ou ambiente) e faça-o valer igual nos dois builds";

    public static IEnumerable<Violation> Check(CompilerInvocation seen, string seenRoot, UnseenBuild unseen) =>
        CheckBuildIsIntact(unseen).Concat(CheckSameInputs(seen, seenRoot, unseen)).Concat(CheckRoutesRefused(unseen));

    /// <summary>Every input the compiler gets in one build and not in the other, named by kind and place.</summary>
    public static IEnumerable<Violation> CheckSameInputs(CompilerInvocation seen, string seenRoot, UnseenBuild unseen)
    {
        var guard = CompilerInputs.Of(seen, seenRoot).Kinds;
        var gate = CompilerInputs.Of(unseen.Invocation, unseen.RepositoryRoot).Kinds;
        return guard.Keys.SelectMany(kind =>
            gate[kind].Except(guard[kind]).Select(item => $"{kind} {item} entra no build igual ao do gate e não no build que a guarda vê")
                .Concat(guard[kind].Except(gate[kind]).Select(item => $"{kind} {item} entra no build que a guarda vê e não no build igual ao do gate")))
            .Select(message => new Violation(unseen.Invocation.ProjectPath, 1, $"{message}: {Hint}"));
    }

    /// <summary>Each banned route appended to a source the gate compiles, and not refused with the banned-symbol diagnostic there.</summary>
    public static IEnumerable<Violation> CheckRoutesRefused(UnseenBuild unseen)
    {
        var compiled = unseen.Invocation.Sources.Select(source => Path.GetRelativePath(unseen.RepositoryRoot, source).Replace(Path.DirectorySeparatorChar, '/')).ToHashSet(StringComparer.Ordinal);
        var refusals = unseen.Build.Lines.Where(line => line.Contains("error " + LoggingSuppressionRules.Diagnostic, StringComparison.Ordinal)).ToList();
        return unseen.Sites.Where(site => compiled.Contains(site.Source))
            .SelectMany(site => site.RouteLines.Select((line, route) => (Site: site, Line: line, Route: LoggingCanary.Routes[route])))
            .Where(planted => !refusals.Exists(refusal => refusal.Contains($"{planted.Site.CanaryPath}({planted.Line},", StringComparison.Ordinal)))
            .Select(planted => new Violation(
                planted.Site.Source,
                planted.Line,
                $"a rota {planted.Route}, acrescentada a esta fonte, não foi recusada com {LoggingSuppressionRules.Diagnostic} no build igual ao do gate: {Hint}"));
    }

    /// <summary>Build errors other than the banned symbol, so that a build that broke never passes for one that refused.</summary>
    public static IEnumerable<Violation> CheckBuildIsIntact(UnseenBuild unseen) =>
        LoggingCanaryRules.CheckBuildIsIntact(unseen.Build)
            .Select(line => new Violation(unseen.Invocation.ProjectPath, 1, $"o build igual ao do gate falhou por outro motivo além de {LoggingSuppressionRules.Diagnostic}: {line}"));
}

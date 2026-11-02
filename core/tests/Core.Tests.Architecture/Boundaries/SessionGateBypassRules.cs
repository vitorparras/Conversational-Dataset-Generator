using Core.Tests.Architecture.CodeRules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Core.Tests.Architecture.Boundaries;

/// <summary>
/// Every route of the host is behind the session gate unless it is marked open, so nothing in the host may answer around the gate and only the entry and the status may
/// be marked open. A short-circuited endpoint skips the middleware that follows it, and a branch or a terminal middleware answers with no endpoint at all, which the
/// gate lets through so that a path nobody serves gets its 404; the inventory of mapped endpoints cannot see either.
/// </summary>
internal static class SessionGateBypassRules
{
    private const string Map = "Map";
    private const string OpenMark = "SessionOpen";
    private const string ShortCircuitMark = "ShortCircuitMetadata";

    /// <summary>The calls that serve a request around the gate: a short circuit of the endpoint, a branch of the pipeline by path or by condition and a terminal middleware.</summary>
    private static readonly string[] Bypasses = ["ShortCircuit", "MapShortCircuit", "MapWhen", "UseWhen", "Run"];

    /// <summary>The only files that may mark a route open: the entry, which makes the session, and the status, which says whether the host is alive.</summary>
    private static readonly string[] MayMarkOpen = ["SessionEndpoint.cs", "StatusEndpoint.cs"];

    /// <summary>Every bypass, every use of the short circuit mark and every open mark in a file that may not make one.</summary>
    public static IEnumerable<Violation> Check(IReadOnlyList<SourceFile> files)
    {
        var ownMappers = files.SelectMany(file => file.Root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            .Where(type => type.Members.OfType<MethodDeclarationSyntax>().Any(method => method.Identifier.ValueText == Map))
            .Select(type => type.Identifier.ValueText)
            .ToHashSet(StringComparer.Ordinal);
        return files.SelectMany(file => Bypassing(file, ownMappers).Concat(Marking(file)))
            .OrderBy(violation => violation.File, StringComparer.Ordinal)
            .ThenBy(violation => violation.Line);
    }

    private static IEnumerable<Violation> Bypassing(SourceFile file, ICollection<string> ownMappers) =>
        file.Root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Select(call => (Call: call, Name: CalledName(call.Expression)))
            .Where(entry => Bypasses.Contains(entry.Name, StringComparer.Ordinal) || (entry.Name == Map && !IsOwnMapper(entry.Call, ownMappers)))
            .Select(entry => new Violation(file.Path, SourceFile.LineOf(entry.Call), $"{entry.Name} serves a request around the session gate"))
            .Concat(file.Root.DescendantNodes().OfType<IdentifierNameSyntax>()
                .Where(name => name.Identifier.ValueText == ShortCircuitMark)
                .Select(name => new Violation(file.Path, SourceFile.LineOf(name), $"{ShortCircuitMark} skips the session gate")));

    private static IEnumerable<Violation> Marking(SourceFile file) =>
        MayMarkOpen.Contains(file.FileName, StringComparer.Ordinal)
            ? []
            : file.Root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>()
                .Where(creation => creation.Type is IdentifierNameSyntax { Identifier.ValueText: OpenMark })
                .Select(creation => new Violation(file.Path, SourceFile.LineOf(creation), $"{OpenMark} marks a route as open outside {string.Join(" and ", MayMarkOpen)}"));

    /// <summary>
    /// A call to the <c>Map</c> a host class declares for its own routes, as <c>StatusEndpoint.Map(app)</c>, or to the one of the class it is in;
    /// any other <c>Map</c> is a branch by path.
    /// </summary>
    private static bool IsOwnMapper(InvocationExpressionSyntax call, ICollection<string> ownMappers) => call.Expression switch
    {
        MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax owner } => ownMappers.Contains(owner.Identifier.ValueText),
        IdentifierNameSyntax => call.Ancestors().OfType<TypeDeclarationSyntax>().Any(type => ownMappers.Contains(type.Identifier.ValueText)),
        _ => false,
    };

    private static string? CalledName(ExpressionSyntax expression) => expression switch
    {
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
        SimpleNameSyntax name => name.Identifier.ValueText,
        _ => null,
    };
}

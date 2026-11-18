using Core.Tests.Architecture.CodeRules;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Core.Tests.Architecture.Boundaries;

/// <summary>
/// A route of the host takes the body of a form and the person of the session from the reader (<c>RequestReader</c>), which is the only file that asks the request whether it carries a form, reads
/// the form and resolves the store of sessions by the cookie in one step. A route that does it by hand reads the body without the cancellation of the request, or throws on a body that is not a
/// form, and each of them answers differently.
/// </summary>
internal static class RequestReaderRules
{
    private const string ReaderFile = "RequestReader.cs";
    private const string Resolve = "Resolve";
    private const string CookieReader = "SessionCookie";

    /// <summary>The members of the request that read the form.</summary>
    private static readonly string[] FormMembers = ["HasFormContentType", "ReadFormAsync", "Form"];

    /// <summary>Every read of the form, and every resolution of the session by the cookie in one step, in a file other than the reader.</summary>
    public static IEnumerable<Violation> Check(IEnumerable<SourceFile> files) =>
        files.Where(file => !string.Equals(file.FileName, ReaderFile, StringComparison.Ordinal))
            .SelectMany(file => ReadingTheForm(file).Concat(ResolvingByHand(file)))
            .OrderBy(violation => violation.File, StringComparer.Ordinal)
            .ThenBy(violation => violation.Line);

    private static IEnumerable<Violation> ReadingTheForm(SourceFile file) =>
        file.Root.DescendantNodes().OfType<MemberAccessExpressionSyntax>()
            .Where(access => FormMembers.Contains(access.Name.Identifier.ValueText, StringComparer.Ordinal) && IsOfTheRequest(access.Expression))
            .Select(access => new Violation(file.Path, SourceFile.LineOf(access), $"{access.Name.Identifier.ValueText} reads the form by hand; the reader is the only way ({ReaderFile})"));

    private static IEnumerable<Violation> ResolvingByHand(SourceFile file) =>
        file.Root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(call => call.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: Resolve } && ReadsTheCookie(call))
            .Select(call => new Violation(file.Path, SourceFile.LineOf(call), $"the person of the session is resolved by hand; use RequestReader.SessionOf ({ReaderFile})"));

    private static bool IsOfTheRequest(ExpressionSyntax owner) => owner switch
    {
        MemberAccessExpressionSyntax access => string.Equals(access.Name.Identifier.ValueText, "Request", StringComparison.Ordinal),
        IdentifierNameSyntax name => name.Identifier.ValueText.Contains("request", StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    private static bool ReadsTheCookie(InvocationExpressionSyntax call) =>
        call.ArgumentList.Arguments.Any(argument => argument.Expression is InvocationExpressionSyntax
        {
            Expression: MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: CookieReader } },
        });
}

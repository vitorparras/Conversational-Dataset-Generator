using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>The banned-symbol diagnostic stays an error everywhere, suppressed only on a class that holds nothing but generated log messages.</summary>
internal static class LoggingSuppressionRules
{
    public const string Diagnostic = "RS0030";
    private const string ClassSuffix = "LogMessages";
    private const string SuppressionAttribute = "SuppressMessage";
    private const string GeneratedAttribute = "LoggerMessage";
    private const string JustificationArgument = "Justification";
    private const string SeverityKey = "dotnet_diagnostic." + Diagnostic + ".severity";
    private const string RequiredSeverity = "error";

    public static IEnumerable<Violation> CheckSuppressions(SourceFile file) =>
        file.Root.DescendantNodes()
            .OfType<AttributeSyntax>()
            .Where(IsSuppression)
            .SelectMany(attribute => Judge(file, attribute));

    public static IEnumerable<Violation> CheckPragmas(SourceFile file) =>
        file.Root.DescendantNodes(descendIntoTrivia: true)
            .OfType<PragmaWarningDirectiveTriviaSyntax>()
            .Where(pragma => pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword))
            .Where(pragma => pragma.ErrorCodes.Count == 0 || pragma.ErrorCodes.Any(code => code.ToString() == Diagnostic))
            .Select(pragma => new Violation(file.Path, SourceFile.LineOf(pragma), $"#pragma disables {Diagnostic}"));

    public static IEnumerable<Violation> CheckBuildFile(ProjectFile project) =>
        project.Document.Descendants()
            .Where(element => element.Name.LocalName is "NoWarn" or "WarningsNotAsErrors")
            .Where(element => element.Value.Contains(Diagnostic, StringComparison.OrdinalIgnoreCase))
            .Select(element => new Violation(
                project.Path, ((IXmlLineInfo)element).LineNumber, $"{element.Name.LocalName} silences {Diagnostic}"));

    public static IEnumerable<Violation> CheckAnalyzerConfig(AnalyzerConfig config) =>
        config.Lines()
            .Select(line => (Line: line, Severity: SeveritySetBy(line.Text)))
            .Where(entry => entry.Severity is not null and not RequiredSeverity)
            .Select(entry => new Violation(
                config.Path, entry.Line.Number, $"{Diagnostic} set to '{entry.Severity}'; only {RequiredSeverity} is allowed"));

    private static IEnumerable<Violation> Judge(SourceFile file, AttributeSyntax attribute)
    {
        var line = SourceFile.LineOf(attribute);
        var id = SuppressedId(attribute);
        if (id is null)
        {
            return [new Violation(file.Path, line, "the suppressed diagnostic must be a string literal")];
        }

        if (!id.StartsWith(Diagnostic, StringComparison.Ordinal))
        {
            return [];
        }

        if (attribute.Parent is not AttributeListSyntax list || list.Target is not null)
        {
            return [new Violation(file.Path, line, $"{Diagnostic} suppressed for the whole assembly or module")];
        }

        return list.Parent is ClassDeclarationSyntax type
            ? JudgeClass(file, attribute, type)
            : [new Violation(file.Path, line, $"{Diagnostic} suppressed on a member; only a class of generated log messages may suppress it")];
    }

    private static IEnumerable<Violation> JudgeClass(SourceFile file, AttributeSyntax attribute, ClassDeclarationSyntax type)
    {
        var line = SourceFile.LineOf(attribute);
        if (!HasJustification(attribute))
        {
            yield return new Violation(file.Path, line, $"{Diagnostic} suppressed without a {JustificationArgument}");
        }

        if (!type.Identifier.ValueText.EndsWith(ClassSuffix, StringComparison.Ordinal))
        {
            yield return new Violation(file.Path, line, $"{Diagnostic} suppressed on {type.Identifier.ValueText}, which is not a *{ClassSuffix} class");
        }

        if (!(type.Modifiers.Any(SyntaxKind.StaticKeyword) && type.Modifiers.Any(SyntaxKind.PartialKeyword)))
        {
            yield return new Violation(file.Path, line, $"{Diagnostic} suppressed on a class that is not static partial");
        }

        foreach (var member in type.Members.Where(member => !IsGeneratedLogMessage(member)))
        {
            yield return new Violation(
                file.Path, SourceFile.LineOf(member), $"member that is not a partial [{GeneratedAttribute}] method in a class that suppresses {Diagnostic}");
        }
    }

    private static bool IsSuppression(AttributeSyntax attribute) =>
        ServiceTypeIndex.SimpleName(attribute.Name) is SuppressionAttribute or SuppressionAttribute + "Attribute";

    /// <summary>The second positional argument as written, or null when it is not a string literal.</summary>
    private static string? SuppressedId(AttributeSyntax attribute) =>
        attribute.ArgumentList?.Arguments
            .Where(argument => argument.NameEquals is null && argument.NameColon is null)
            .Skip(1)
            .FirstOrDefault()?.Expression is LiteralExpressionSyntax literal
            ? literal.Token.ValueText
            : null;

    private static bool HasJustification(AttributeSyntax attribute) =>
        attribute.ArgumentList?.Arguments.Any(argument =>
            argument.NameEquals?.Name.Identifier.ValueText == JustificationArgument
            && argument.Expression is LiteralExpressionSyntax { Token.ValueText.Length: > 0 }) == true;

    private static bool IsGeneratedLogMessage(MemberDeclarationSyntax member) =>
        member is MethodDeclarationSyntax { Body: null, ExpressionBody: null } method
        && method.Modifiers.Any(SyntaxKind.PartialKeyword)
        && method.AttributeLists.SelectMany(list => list.Attributes).Any(attribute =>
            ServiceTypeIndex.SimpleName(attribute.Name) is GeneratedAttribute or GeneratedAttribute + "Attribute");

    /// <summary>The severity a config line gives the diagnostic, or null when the line is about something else.</summary>
    private static string? SeveritySetBy(string text)
    {
        var parts = text.Split('=', 2);
        return parts.Length == 2 && parts[0].Trim().Equals(SeverityKey, StringComparison.OrdinalIgnoreCase)
            ? parts[1].Trim().ToLowerInvariant()
            : null;
    }
}

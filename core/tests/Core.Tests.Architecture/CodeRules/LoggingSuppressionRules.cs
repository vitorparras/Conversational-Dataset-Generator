using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>The banned-symbol diagnostic stays an error everywhere, suppressed only on a class that holds nothing but generated log messages.</summary>
internal static class LoggingSuppressionRules
{
    public const string Diagnostic = "RS0030";
    public const string AnalyzerPackage = "Microsoft.CodeAnalysis.BannedApiAnalyzers";
    public const string BannedSymbolsFile = "BannedSymbols.txt";
    private const string ClassSuffix = "LogMessages";
    private const string GeneratedAttribute = "LoggerMessage";
    private const string JustificationArgument = "Justification";
    private const string SeverityKey = "dotnet_diagnostic." + Diagnostic + ".severity";
    private const string RequiredSeverity = "error";
    private static readonly string[] SuppressionAttributes = ["SuppressMessage", "UnconditionalSuppressMessage"];

    public static IEnumerable<Violation> CheckSuppressions(SourceFile file) =>
        Suppressions(file).SelectMany(attribute => Judge(file, attribute));

    /// <summary>Every declaration of a class that suppresses the diagnostic, in whichever file, holds only generated log messages.</summary>
    public static IEnumerable<Violation> CheckSuppressedClassDeclarations(IReadOnlyList<SourceFile> files)
    {
        var suppressed = files
            .SelectMany(file => Suppressions(file).Select(attribute => attribute.Parent?.Parent))
            .OfType<ClassDeclarationSyntax>()
            .Select(QualifiedName)
            .ToHashSet(StringComparer.Ordinal);
        return files.SelectMany(file => file.Root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(type => suppressed.Contains(QualifiedName(type)))
            .SelectMany(type => JudgeDeclaration(file, type)));
    }

    public static IEnumerable<Violation> CheckPragmas(SourceFile file) =>
        file.Root.DescendantNodes(descendIntoTrivia: true)
            .OfType<PragmaWarningDirectiveTriviaSyntax>()
            .Where(pragma => pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword))
            .Where(pragma => pragma.ErrorCodes.Count == 0 || pragma.ErrorCodes.Any(code => code.ToString() == Diagnostic))
            .Select(pragma => new Violation(file.Path, SourceFile.LineOf(pragma), $"#pragma disables {Diagnostic}"));

    public static IEnumerable<Violation> CheckBuildFile(ProjectFile project) =>
        project.Document.Descendants()
            .Select(element => (Element: element, Problem: BuildProblem(element)))
            .Where(entry => entry.Problem is not null)
            .Select(entry => new Violation(project.Path, ((IXmlLineInfo)entry.Element).LineNumber, entry.Problem!));

    public static IEnumerable<Violation> CheckAnalyzerConfig(AnalyzerConfig config) =>
        config.Lines()
            .Select(line => (Line: line, Severity: SeveritySetBy(line.Text)))
            .Where(entry => entry.Severity is not null and not RequiredSeverity)
            .Select(entry => new Violation(
                config.Path, entry.Line.Number, $"{Diagnostic} set to '{entry.Severity}'; only {RequiredSeverity} is allowed"));

    private static IEnumerable<AttributeSyntax> Suppressions(SourceFile file) =>
        file.Root.DescendantNodes()
            .OfType<AttributeSyntax>()
            .Where(attribute => SuppressionAttributes.Any(name => ServiceTypeIndex.SimpleName(attribute.Name) == name || ServiceTypeIndex.SimpleName(attribute.Name) == name + "Attribute"))
            .Where(attribute => SuppressedId(attribute) is null || SuppressedId(attribute)!.StartsWith(Diagnostic, StringComparison.Ordinal));

    private static IEnumerable<Violation> Judge(SourceFile file, AttributeSyntax attribute)
    {
        var line = SourceFile.LineOf(attribute);
        if (SuppressedId(attribute) is null)
        {
            return [new Violation(file.Path, line, "the suppressed diagnostic must be a string literal")];
        }

        if (attribute.Parent is not AttributeListSyntax list || list.Target is not null)
        {
            return [new Violation(file.Path, line, $"{Diagnostic} suppressed for the whole assembly or module")];
        }

        return list.Parent is ClassDeclarationSyntax type
            ? JudgeSite(file, attribute, type)
            : [new Violation(file.Path, line, $"{Diagnostic} suppressed on a member; only a class of generated log messages may suppress it")];
    }

    private static IEnumerable<Violation> JudgeSite(SourceFile file, AttributeSyntax attribute, ClassDeclarationSyntax type)
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

        foreach (var violation in JudgeDeclaration(file, type))
        {
            yield return violation;
        }
    }

    /// <summary>One declaration of a suppressing class: static partial, with nothing but partial generated methods inside.</summary>
    private static IEnumerable<Violation> JudgeDeclaration(SourceFile file, ClassDeclarationSyntax type)
    {
        if (!(type.Modifiers.Any(SyntaxKind.StaticKeyword) && type.Modifiers.Any(SyntaxKind.PartialKeyword)))
        {
            yield return new Violation(file.Path, SourceFile.LineOf(type), $"{Diagnostic} suppressed on a class that is not static partial");
        }

        foreach (var member in type.Members.Where(member => !IsGeneratedLogMessage(member)))
        {
            yield return new Violation(
                file.Path, SourceFile.LineOf(member), $"member that is not a partial [{GeneratedAttribute}] method in a class that suppresses {Diagnostic}");
        }
    }

    private static string QualifiedName(ClassDeclarationSyntax type) =>
        string.Join(".", type.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Select(ns => ns.Name.ToString()).Reverse().Append(type.Identifier.ValueText));

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

    /// <summary>What a build element does against the ban, or null when it is harmless.</summary>
    private static string? BuildProblem(System.Xml.Linq.XElement element)
    {
        var name = element.Name.LocalName;
        var removed = element.Attribute("Remove")?.Value ?? string.Empty;
        return name switch
        {
            "NoWarn" or "WarningsNotAsErrors" when element.Value.Contains(Diagnostic, StringComparison.OrdinalIgnoreCase) => $"{name} silences {Diagnostic}",
            "AdditionalFiles" when removed.Contains(BannedSymbolsFile, StringComparison.OrdinalIgnoreCase) => $"{BannedSymbolsFile} removed from the analyzer's input",
            "PackageReference" when removed.Equals(AnalyzerPackage, StringComparison.OrdinalIgnoreCase) => $"{AnalyzerPackage} removed from the project",
            _ => null,
        };
    }

    /// <summary>The severity a config line gives the diagnostic, or null when the line is about something else.</summary>
    private static string? SeveritySetBy(string text)
    {
        var parts = text.Split('=', 2);
        return parts.Length == 2 && parts[0].Trim().Equals(SeverityKey, StringComparison.OrdinalIgnoreCase)
            ? parts[1].Trim().ToLowerInvariant()
            : null;
    }
}

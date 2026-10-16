using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>The banned-symbol diagnostic is suppressed only on a class that holds nothing but generated log messages.</summary>
internal static class LoggingSuppressionRules
{
    public const string Diagnostic = "RS0030";
    private const string ClassSuffix = "LogMessages";
    private const string GeneratedAttribute = "LoggerMessage";
    private const string JustificationArgument = "Justification";

    /// <summary>Every attribute that binds to a suppression type, however it is aliased, and every declaration of the class it sits on.</summary>
    public static IEnumerable<Violation> CheckSuppressions(IReadOnlyList<SourceFile> files)
    {
        var compilation = SourceCompilation.Of(files);
        var found = files
            .SelectMany(file => Suppression.FindIn(file, compilation.GetSemanticModel(file.Tree)))
            .Where(suppression => suppression.CheckId is null || suppression.CheckId.StartsWith(Diagnostic, StringComparison.Ordinal))
            .ToList();
        var suppressingClasses = found.Select(suppression => suppression.AttributedClass).OfType<INamedTypeSymbol>().ToHashSet(SymbolEqualityComparer.Default);
        return found.SelectMany(JudgeAttribute)
            .Concat(files.SelectMany(file => JudgeDeclarations(file, compilation.GetSemanticModel(file.Tree), suppressingClasses)));
    }

    public static IEnumerable<Violation> CheckPragmas(SourceFile file) =>
        file.Root.DescendantNodes(descendIntoTrivia: true)
            .OfType<PragmaWarningDirectiveTriviaSyntax>()
            .Where(pragma => pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword))
            .Where(pragma => pragma.ErrorCodes.Count == 0 || pragma.ErrorCodes.Any(code => code.ToString() == Diagnostic))
            .Select(pragma => new Violation(file.Path, SourceFile.LineOf(pragma), $"#pragma disables {Diagnostic}"));

    private static IEnumerable<Violation> JudgeAttribute(Suppression suppression)
    {
        var path = suppression.File.Path;
        if (!suppression.IdIsLiteral)
        {
            return [new Violation(path, suppression.Line, "the suppressed diagnostic must be a string literal")];
        }

        if (suppression.Attribute.Parent is not AttributeListSyntax { Target: null } list)
        {
            return [new Violation(path, suppression.Line, $"{Diagnostic} suppressed for the whole assembly or module")];
        }

        return list.Parent is ClassDeclarationSyntax type
            ? JudgeSite(suppression, type)
            : [new Violation(path, suppression.Line, $"{Diagnostic} suppressed on a member; only a class of generated log messages may suppress it")];
    }

    private static IEnumerable<Violation> JudgeSite(Suppression suppression, ClassDeclarationSyntax type)
    {
        if (!HasJustification(suppression.Attribute))
        {
            yield return new Violation(suppression.File.Path, suppression.Line, $"{Diagnostic} suppressed without a {JustificationArgument}");
        }

        if (!type.Identifier.ValueText.EndsWith(ClassSuffix, StringComparison.Ordinal))
        {
            yield return new Violation(
                suppression.File.Path, suppression.Line, $"{Diagnostic} suppressed on {type.Identifier.ValueText}, which is not a *{ClassSuffix} class");
        }
    }

    /// <summary>Each declaration of a suppressing class, in whichever file: static partial, with nothing but partial generated methods inside.</summary>
    private static IEnumerable<Violation> JudgeDeclarations(SourceFile file, SemanticModel model, HashSet<ISymbol?> suppressingClasses) =>
        file.Root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(type => suppressingClasses.Contains(model.GetDeclaredSymbol(type)))
            .SelectMany(type => JudgeDeclaration(file, type));

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

    private static bool HasJustification(AttributeSyntax attribute) =>
        attribute.ArgumentList?.Arguments.Any(argument =>
            argument.NameEquals?.Name.Identifier.ValueText == JustificationArgument
            && argument.Expression is LiteralExpressionSyntax { Token.ValueText.Length: > 0 }) == true;

    private static bool IsGeneratedLogMessage(MemberDeclarationSyntax member) =>
        member is MethodDeclarationSyntax { Body: null, ExpressionBody: null } method
        && method.Modifiers.Any(SyntaxKind.PartialKeyword)
        && method.AttributeLists.SelectMany(list => list.Attributes).Any(attribute =>
            ServiceTypeIndex.SimpleName(attribute.Name) is GeneratedAttribute or GeneratedAttribute + "Attribute");
}

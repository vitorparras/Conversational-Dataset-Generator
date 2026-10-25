using Core.Tests.Architecture.CodeRules;

namespace Core.Tests.Architecture.Boundaries;

/// <summary>Which project of the core is compiled against which, read from what the build hands the compiler rather than from how a project file is written.</summary>
internal static class ModuleGraph
{
    public static IReadOnlyList<CompilerInvocation> Invocations(IEnumerable<ProjectFile> projects) =>
        [.. projects.Select(project => CompilerInvocation.Of(project.Path))];

    /// <summary>The referencing side is named by its project file, which no property inside it can rename.</summary>
    public static IEnumerable<ModuleReference> Of(CompilerInvocation invocation) =>
        invocation.ProjectReferences.Select(reference => new ModuleReference(ProjectName(invocation), reference));

    public static string ProjectName(CompilerInvocation invocation) => Path.GetFileNameWithoutExtension(invocation.ProjectPath);
}

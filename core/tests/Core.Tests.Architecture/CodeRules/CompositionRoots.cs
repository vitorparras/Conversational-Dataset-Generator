namespace Core.Tests.Architecture.CodeRules;

/// <summary>Where each executable assembles itself: the one <c>CompositionRoot.cs</c> beside its project file.</summary>
internal sealed class CompositionRoots
{
    public const string FileName = "CompositionRoot.cs";

    private readonly IReadOnlyList<ProjectFile> executables;

    private CompositionRoots(IReadOnlyList<ProjectFile> executables)
    {
        this.executables = executables;
    }

    public static CompositionRoots From(IEnumerable<ProjectFile> projects) =>
        new([.. projects.Where(project => project.IsExecutable)]);

    public bool IsRoot(SourceFile file) => executables.Any(executable => SamePath(file.Path, RootPathOf(executable)));

    public IEnumerable<Violation> CheckOnePerExecutable(IEnumerable<SourceFile> files)
    {
        var candidates = files.Where(file => file.FileName == FileName).ToList();
        var missing = executables
            .Where(executable => !candidates.Exists(candidate => SamePath(candidate.Path, RootPathOf(executable))))
            .Select(executable => new Violation(
                executable.Path, 1, $"executable without a {FileName} beside its project file"));
        var misplaced = candidates
            .Where(candidate => !IsRoot(candidate))
            .Select(candidate => new Violation(
                candidate.Path, 1, $"{FileName} outside the root of an executable; one root per executable"));
        return missing.Concat(misplaced);
    }

    private static string RootPathOf(ProjectFile executable) => Path.Combine(executable.DirectoryPath, FileName);

    private static bool SamePath(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
}

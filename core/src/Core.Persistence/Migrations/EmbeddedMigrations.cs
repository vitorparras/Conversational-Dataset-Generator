using System.Reflection;

namespace Core.Persistence.Migrations;

/// <summary>The steps this build carries: the <c>Sql/name.sql</c> resources, in the order of <see cref="MigrationNames"/>; a step's version is its position, from one.</summary>
public static class EmbeddedMigrations
{
    private const string Folder = "Sql/";
    private const string Extension = ".sql";

    /// <summary>Every embedded step; a resource that is named in neither format is a defect of the build.</summary>
    public static IReadOnlyList<MigrationStep> Load()
    {
        var assembly = typeof(EmbeddedMigrations).Assembly;
        var names = MigrationNames.Ordered(assembly.GetManifestResourceNames()
            .Where(resource => resource.StartsWith(Folder, StringComparison.Ordinal) && resource.EndsWith(Extension, StringComparison.Ordinal))
            .Select(resource => resource[Folder.Length..^Extension.Length]));
        return [.. names.Select((name, index) => StepOf(assembly, name, index + 1))];
    }

    private static MigrationStep StepOf(Assembly assembly, string name, int version)
    {
        using var stream = assembly.GetManifestResourceStream(Folder + name + Extension)!;
        using var content = new MemoryStream();
        stream.CopyTo(content);
        return MigrationStep.Of(version, name, content.ToArray());
    }
}

using System.Globalization;
using System.Reflection;

namespace Core.Persistence.Migrations;

/// <summary>The steps this build carries: the <c>Sql/NNNN-name.sql</c> resources, in numeric order.</summary>
public static class EmbeddedMigrations
{
    private const string Folder = "Sql/";
    private const string Extension = ".sql";
    private const int NumberWidth = 4;

    /// <summary>Every embedded step; a resource that does not start with its four-digit number is a defect of the build.</summary>
    public static IReadOnlyList<MigrationStep> Load()
    {
        var assembly = typeof(EmbeddedMigrations).Assembly;
        return [.. assembly.GetManifestResourceNames()
            .Where(resource => resource.StartsWith(Folder, StringComparison.Ordinal) && resource.EndsWith(Extension, StringComparison.Ordinal))
            .Select(resource => StepOf(assembly, resource))
            .OrderBy(step => step.Version)];
    }

    private static MigrationStep StepOf(Assembly assembly, string resource)
    {
        var name = resource[Folder.Length..^Extension.Length];
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var content = new MemoryStream();
        stream.CopyTo(content);
        return MigrationStep.Of(int.Parse(name[..NumberWidth], NumberStyles.None, CultureInfo.InvariantCulture), name, content.ToArray());
    }
}

namespace Core.Persistence.Migrations;

/// <summary>The registry set against the steps of this build in order: the last version of each, and how many steps are applied.</summary>
internal sealed class SchemaComparison
{
    internal required SchemaState State { get; init; }
    internal required int Found { get; init; }
    internal required int Expected { get; init; }

    internal int Applied { get; init; }
    internal string? Step { get; init; }
    internal string? Registered { get; init; }
    internal string? Current { get; init; }

    internal static SchemaComparison Compare(IReadOnlyList<MigrationStep> steps, IReadOnlyList<AppliedStep> applied)
    {
        var found = applied.Count == 0 ? 0 : applied[^1].Version;
        var expected = steps.Count == 0 ? 0 : steps[^1].Version;
        var place = Enumerable.Range(0, applied.Count)
            .Where(index => index >= steps.Count || applied[index].Version != steps[index].Version || applied[index].Sha256 != steps[index].Sha256)
            .Cast<int?>()
            .FirstOrDefault();
        return place switch
        {
            null => new() { State = applied.Count == steps.Count ? SchemaState.UpToDate : SchemaState.Behind, Found = found, Expected = expected, Applied = applied.Count },
            var index when index >= steps.Count => new() { State = SchemaState.Ahead, Found = found, Expected = expected },
            var index => new()
            {
                State = SchemaState.Divergent,
                Found = found,
                Expected = expected,
                Step = steps[index.Value].Name,
                Registered = applied[index.Value].Sha256,
                Current = steps[index.Value].Sha256,
            },
        };
    }
}

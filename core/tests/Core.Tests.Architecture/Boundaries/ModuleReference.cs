namespace Core.Tests.Architecture.Boundaries;

/// <summary>One project of the core compiled against another, both named by their assemblies.</summary>
internal sealed record ModuleReference(string From, string To)
{
    public override string ToString() => $"{From} -> {To}";
}

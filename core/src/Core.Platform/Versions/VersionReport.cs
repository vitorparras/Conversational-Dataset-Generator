namespace Core.Platform.Versions;

/// <summary>
/// The versions the system runs under. A version the system has no way to obtain yet is absent, never empty and
/// never guessed: the contract and the language service fill theirs when the core speaks to the service, and the
/// schema fills its own when the core has a database.
/// </summary>
public sealed record VersionReport
{
    /// <summary>The report of an executable built as <paramref name="core"/> says.</summary>
    public VersionReport(BuildInfo core)
    {
        ArgumentNullException.ThrowIfNull(core);
        CoreVersion = core.Version;
        Commit = core.Commit;
        WorkingTree = core.WorkingTree;
    }

    /// <summary>The package version of the core.</summary>
    public VersionValue CoreVersion { get; }

    /// <summary>The commit the core was built from.</summary>
    public VersionValue Commit { get; }

    /// <summary>Whether the core was built from a working tree with changes not yet committed.</summary>
    public WorkingTree WorkingTree { get; }

    /// <summary>The version of the contract between the core and the language service.</summary>
    public VersionValue ContractVersion { get; } = VersionValue.Absent;

    /// <summary>The version the language service reports of itself.</summary>
    public VersionValue LanguageServiceVersion { get; } = VersionValue.Absent;

    /// <summary>The version of the model the language service reports.</summary>
    public VersionValue ModelVersion { get; } = VersionValue.Absent;

    /// <summary>The version of the database schema.</summary>
    public VersionValue SchemaVersion { get; } = VersionValue.Absent;
}

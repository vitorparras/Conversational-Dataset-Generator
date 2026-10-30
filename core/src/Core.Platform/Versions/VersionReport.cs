namespace Core.Platform.Versions;

/// <summary>
/// The versions the system runs under. A version the system has no way to obtain is absent, never empty and
/// never guessed: the contract, the language service and the model are the ones the service declares, read from it
/// for each report and absent when it gives none, and the schema is the one the startup check confirmed, absent when the core has no database.
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

    /// <summary>This report with the versions the language service declared; the report itself does not change.</summary>
    public VersionReport WithServiceVersions(ServiceVersions service)
    {
        ArgumentNullException.ThrowIfNull(service);
        return this with { ContractVersion = service.Contract, LanguageServiceVersion = service.Service, ModelVersion = service.Model };
    }

    /// <summary>This report with the schema version the startup check confirmed; the report itself does not change.</summary>
    public VersionReport WithSchemaVersion(VersionValue schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        return this with { SchemaVersion = schema };
    }

    /// <summary>This report with the installation the process declares; the report itself does not change.</summary>
    public VersionReport WithInstallation(Installation installation)
    {
        ArgumentNullException.ThrowIfNull(installation);
        return this with { Installation = installation };
    }

    /// <summary>The package version of the core.</summary>
    public VersionValue CoreVersion { get; }

    /// <summary>The commit the core was built from.</summary>
    public VersionValue Commit { get; }

    /// <summary>Whether the core was built from a working tree with changes not yet committed.</summary>
    public WorkingTree WorkingTree { get; }

    /// <summary>What the installation declares itself to be; null while the report has not been given one.</summary>
    public Installation? Installation { get; private init; }

    /// <summary>The version of the contract the language service declares, not the one the core speaks.</summary>
    public VersionValue ContractVersion { get; private init; } = VersionValue.Absent;

    /// <summary>The version the language service reports of itself.</summary>
    public VersionValue LanguageServiceVersion { get; private init; } = VersionValue.Absent;

    /// <summary>The version of the model the language service reports.</summary>
    public VersionValue ModelVersion { get; private init; } = VersionValue.Absent;

    /// <summary>The version of the database schema.</summary>
    public VersionValue SchemaVersion { get; private init; } = VersionValue.Absent;
}

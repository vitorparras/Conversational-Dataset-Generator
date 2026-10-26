using System.Collections;
using Core.Persistence.Migrations;
using Core.Platform.Versions;

namespace Core.Host;

/// <summary>The versions the host runs under once the startup checks have passed, fixed at that moment and not read again.</summary>
internal static class StartupVersions
{
    /// <summary>The report of this build with the schema version the check confirmed; absent when no database was declared, so no check ran.</summary>
    internal static VersionReport Of(IEnumerable checks) =>
        new VersionReport(BuildInfo.Of(typeof(StartupVersions).Assembly))
            .WithSchemaVersion(checks.OfType<SchemaVersionCheck>().SingleOrDefault()?.ConfirmedVersion ?? VersionValue.Absent);
}

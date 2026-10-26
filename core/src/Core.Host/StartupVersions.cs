using System.Collections;
using Core.Boundary.Http;
using Core.Persistence.Migrations;
using Core.Platform.Versions;

namespace Core.Host;

/// <summary>The versions the host runs under once the startup checks have passed: the build and the schema fixed at that moment, the service's read once for the startup line.</summary>
internal static class StartupVersions
{
    /// <summary>The report of this build with the schema version the check confirmed; absent when no database was declared, so no check ran.</summary>
    internal static VersionReport Of(IEnumerable checks) =>
        new VersionReport(BuildInfo.Of(typeof(StartupVersions).Assembly))
            .WithSchemaVersion(checks.OfType<SchemaVersionCheck>().SingleOrDefault()?.ConfirmedVersion ?? VersionValue.Absent);

    /// <summary>Writes the startup line with the versions the service declares now; a service that gives none leaves them absent, and the wait is the limit of the read.</summary>
    internal static async Task WriteAsync(ILogger logger, VersionReport report, IServiceVersionReader service, CancellationToken cancellationToken)
    {
        var inForce = report.WithServiceVersions(await service.ReadAsync(cancellationToken));
        logger.VersionsInForce(inForce);
    }
}

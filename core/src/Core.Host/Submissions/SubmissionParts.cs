using Core.Boundary.Text;
using Core.Ingestion;
using Core.Ingestion.Entrance;
using Core.Ingestion.Reading;
using Core.Persistence.Content;
using Core.Persistence.Content.Sealing;
using Core.Persistence.Content.Writing;
using Core.Persistence.Domains;
using Core.Persistence.Ingestion;
using Core.Platform.Keys;
using Npgsql;

namespace Core.Host.Submissions;

/// <summary>
/// Where the pieces of the submissions are taken from the configuration: the batches open under the list of conditions the installation declared,
/// so that an omitted condition takes its first, and the service of the submissions takes files up to the most bytes the installation declared
/// and seals the origin with the key the host reads when the first submission comes.
/// </summary>
internal static class SubmissionParts
{
    /// <summary>The batches over the database, under the conditions the configuration gave, the most restrictive first.</summary>
    internal static IngestionBatches BatchesOf(NpgsqlDataSource source, IReadOnlyDictionary<string, string> values) =>
        new(source, AdmissionConfiguration.Conditions(values));

    /// <summary>The batches over the database the startup container holds; none when no database is declared.</summary>
    internal static IngestionBatches BatchesOf(IServiceProvider provider, IReadOnlyDictionary<string, string> values) =>
        provider.GetRequiredService<ServiceProvider>().GetService<NpgsqlDataSource>() is { } source ? BatchesOf(source, values) : null!;

    /// <summary>The register of the batches over the database the startup container holds; none when no database is declared.</summary>
    internal static BatchRegister RegisterOf(IServiceProvider provider) =>
        provider.GetRequiredService<ServiceProvider>().GetService<NpgsqlDataSource>() is { } source ? new BatchRegister(source) : null!;

    /// <summary>
    /// The holder of the service, which reads the master key and the labels when the first submission asks and holds the key until the host stops;
    /// none when no database is declared.
    /// </summary>
    internal static SubmissionServiceHolder ServicesOf(IServiceProvider provider, IReadOnlyDictionary<string, string> values) =>
        provider.GetRequiredService<ServiceProvider>() is var startup && startup.GetService<NpgsqlDataSource>() is { } source
            ? new SubmissionServiceHolder(AdmissionConfiguration.MaxBytes(values), token => LoadAsync(startup, source, provider.GetRequiredService<IngestionBatches>(), values, token))
            : null!;

    /// <summary>The service over the key a reading holds, or over none when it failed: it then registers and turns back; the intake and the register are built here.</summary>
    private static SubmissionService ServiceOf(NpgsqlDataSource source, MasterKeyReading reading, SubmissionSettings settings)
    {
        var sealing = reading.Failure is null ? new ProtectedContent(reading.Labels, CompositionRoot.KeysOf(reading), settings.Registered, settings.Revoked) : null;
        var writer = sealing is null ? null : new ContentWriter(source, sealing);
        var marking = sealing is null ? null : new ReplicationMarking(new ReplicationStore(source), new KeyDerivation(reading.Key!, reading.Labels), settings.Replication);
        var intake = new SubmissionIntake(
            new SubmissionGate(SourceReaders.Default(), settings.MaxBytes), new FormConference(new TextBoundaries()), settings.Conditions, sealing, writer, marking);
        return new SubmissionService(intake, settings.Batches, new BatchRegister(source), source);
    }

    private static async Task<SubmissionServiceHolder.Loaded> LoadAsync(
        IServiceProvider startup, NpgsqlDataSource source, IngestionBatches batches, IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken)
    {
        var reading = await startup.GetRequiredService<IMasterKeySource>().ReadAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var failed = reading.Failure is not null;
            IReadOnlySet<string> registered = failed ? new HashSet<string>() : await RegisteredLabels.ReadAsync(source, cancellationToken).ConfigureAwait(false);
            IReadOnlySet<string> revoked = failed ? new HashSet<string>() : await LabelRevocations.ReadAsync(source, cancellationToken).ConfigureAwait(false);
            var settings = new SubmissionSettings(batches, AdmissionConfiguration.Conditions(values), AdmissionConfiguration.MaxBytes(values), registered, revoked, ReplicationConfiguration.Rule(values));
            var service = ServiceOf(source, reading, settings);
            return reading.Failure is { } failure ? throw new ContentKeyUnavailableException(failure.Kind, service) : new SubmissionServiceHolder.Loaded(service, reading);
        }
        catch
        {
            reading.Dispose();
            throw;
        }
    }
}

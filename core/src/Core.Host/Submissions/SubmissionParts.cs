using Core.Persistence.Domains;
using Core.Persistence.Ingestion;
using Core.Platform.Keys;
using Npgsql;

namespace Core.Host.Submissions;

/// <summary>
/// Where the pieces of the submissions are taken from the configuration: the batches open under the list of conditions the installation declared,
/// so that an omitted condition takes its first, and the service of the submissions takes files up to the most bytes the installation declared
/// and seals the origin with the key the host reads at each submission.
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

    /// <summary>The door of the ingestion over the database the startup container holds; none when no database is declared.</summary>
    internal static IntakeDoor DoorOf(IServiceProvider provider) =>
        provider.GetRequiredService<ServiceProvider>().GetService<NpgsqlDataSource>() is { } source ? new IntakeDoor(source) : null!;

    /// <summary>
    /// The holder of the service, which reads the master key and the labels at each submission and lets the reading go at its end;
    /// none when no database is declared.
    /// </summary>
    internal static SubmissionServiceHolder ServicesOf(IServiceProvider provider, IReadOnlyDictionary<string, string> values) =>
        provider.GetRequiredService<ServiceProvider>() is var startup && startup.GetService<NpgsqlDataSource>() is not null
            ? new SubmissionServiceHolder(AdmissionConfiguration.MaxBytes(values), token => LoadAsync(startup, provider.GetRequiredService<IngestionBatches>(), values, token))
            : null!;

    /// <summary>The service over the key a reading holds: the intake is built by the composition root, the register here.</summary>
    private static SubmissionService ServiceOf(NpgsqlDataSource source, MasterKeyReading reading, SubmissionSettings settings) =>
        new(CompositionRoot.IntakeOf(reading, settings), settings.Batches, new BatchRegister(source), source);

    private static async Task<SubmissionServiceHolder.Loaded?> LoadAsync(
        IServiceProvider startup, IngestionBatches batches, IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken)
    {
        var source = startup.GetRequiredService<NpgsqlDataSource>();
        var reading = await startup.GetRequiredService<IMasterKeySource>().ReadAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (reading.Failure is not null)
            {
                reading.Dispose();
                return null;
            }

            var registered = await RegisteredLabels.ReadAsync(source, cancellationToken).ConfigureAwait(false);
            var revoked = await LabelRevocations.ReadAsync(source, cancellationToken).ConfigureAwait(false);
            var settings = new SubmissionSettings(batches, AdmissionConfiguration.Conditions(values), AdmissionConfiguration.MaxBytes(values), registered, revoked);
            return new SubmissionServiceHolder.Loaded(ServiceOf(source, reading, settings), reading);
        }
        catch
        {
            reading.Dispose();
            throw;
        }
    }
}

using Core.Persistence.Domains;
using Core.Persistence.Ingestion;
using Core.Platform.Keys;
using Npgsql;

namespace Core.Host.Submissions;

/// <summary>
/// Where the pieces of the ingestion are taken from the configuration: the batches open under the list of conditions the installation declared, so that an omitted condition takes its first,
/// and the service of the submissions takes files up to the most bytes the installation declared and seals the origin with the key the host reads when the first submission comes.
/// </summary>
internal static class IngestionServices
{
    /// <summary>The batches over the database, under the conditions the configuration gave, the most restrictive first.</summary>
    internal static IngestionBatches BatchesOf(NpgsqlDataSource source, IReadOnlyDictionary<string, string> values) =>
        new(source, IngestionConfiguration.Conditions(values));

    /// <summary>The holder of the service, which reads the master key and the labels when the first submission asks and holds the key until the host stops; none when no database is declared.</summary>
    internal static SubmissionServices ServicesOf(IServiceProvider provider, IReadOnlyDictionary<string, string> values) =>
        provider.GetRequiredService<ServiceProvider>().GetService<NpgsqlDataSource>() is { } source
            ? new SubmissionServices(IngestionConfiguration.MaxBytes(values), token => LoadAsync(provider.GetRequiredService<ServiceProvider>(), source, values, token))
            : null!;

    private static async Task<SubmissionServices.Loaded> LoadAsync(ServiceProvider startup, NpgsqlDataSource source, IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken)
    {
        var reading = await startup.GetRequiredService<IMasterKeySource>().ReadAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (reading.Failure is not null)
            {
                throw new InvalidOperationException("The master key could not be read to seal what a submission declares.");
            }

            var registered = await RegisteredLabels.ReadAsync(source, cancellationToken).ConfigureAwait(false);
            var revoked = await LabelRevocations.ReadAsync(source, cancellationToken).ConfigureAwait(false);
            var settings = new SubmissionSettings(BatchesOf(source, values), IngestionConfiguration.Conditions(values), IngestionConfiguration.MaxBytes(values), registered, revoked);
            return new SubmissionServices.Loaded(CompositionRoot.SubmissionsOf(source, reading, settings), reading);
        }
        catch
        {
            reading.Dispose();
            throw;
        }
    }
}

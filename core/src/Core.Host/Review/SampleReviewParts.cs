using Core.Persistence.Content.Sealing;
using Core.Persistence.Domains;
using Core.Persistence.Ingestion;
using Core.Persistence.People;
using Core.Platform.Keys;
using Npgsql;

namespace Core.Host.Review;

/// <summary>
/// What the desk of the review of the sample takes from the installation: the database the startup container holds, the size of the sample the configuration declares and the master key,
/// which is read at each screen that shows a text and let go of when it ends. With no database there is no desk.
/// </summary>
internal static class SampleReviewParts
{
    /// <summary>The desk over the database the startup container holds; none when no database is declared.</summary>
    internal static SampleReviewDesk DeskOf(IServiceProvider provider, IReadOnlyDictionary<string, string> values) =>
        provider.GetRequiredService<ServiceProvider>() is var startup && startup.GetService<NpgsqlDataSource>() is { } source
            ? new SampleReviewDesk(
                (new BatchReviews(source, AdmissionConfiguration.ReviewSampleSize(values)), new SampleMarks(source), new BatchApprovals(source)),
                (new PersonRegistry(source), token => AccessAsync(startup, source, token), AdmissionConfiguration.ReviewSampleSize(values)))
            : null!;

    private static async Task<SampleTextAccess> AccessAsync(IServiceProvider startup, NpgsqlDataSource source, CancellationToken cancellationToken)
    {
        var reading = await startup.GetRequiredService<IMasterKeySource>().ReadAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (reading.Failure is not null)
            {
                reading.Dispose();
                return new SampleTextAccess(null, null);
            }

            var registered = await RegisteredLabels.ReadAsync(source, cancellationToken).ConfigureAwait(false);
            var content = new ProtectedContent(reading.Labels, CompositionRoot.KeysOf(reading), registered);
            return new SampleTextAccess(new SampleTextReader(source, content), reading);
        }
        catch
        {
            reading.Dispose();
            throw;
        }
    }
}

using Core.Host.Submissions;
using Core.Persistence.Domains;
using Core.Persistence.Ingestion;
using Core.Persistence.People;
using Core.Platform.Keys;
using Npgsql;

namespace Core.Host.Review;

/// <summary>What the desk of the review takes from the installation: the database, the size of the sample the configuration declares and the master key. With no database there is no desk.</summary>
internal static class SampleReviewParts
{
    /// <summary>The desk over the database the startup container holds; none when no database is declared.</summary>
    internal static SampleReviewDesk DeskOf(IServiceProvider provider, IReadOnlyDictionary<string, string> values) =>
        provider.GetRequiredService<ServiceProvider>() is var startup && startup.GetService<NpgsqlDataSource>() is { } source
            ? new SampleReviewDesk(
                (new BatchReviews(source, AdmissionConfiguration.ReviewSampleSize(values)), new SampleMarks(source),
                    new CopyClosing(new BatchApprovals(source), SubmissionParts.BatchesOf(source, values))),
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
            return new SampleTextAccess(CompositionRoot.SampleTextOf(source, reading, registered), reading);
        }
        catch
        {
            reading.Dispose();
            throw;
        }
    }
}

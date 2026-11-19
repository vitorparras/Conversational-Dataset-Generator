using Core.Boundary.Contracts;
using Core.Ingestion;
using Core.Persistence.Ingestion;

namespace Core.Host.Submissions;

/// <summary>
/// What the service of the submissions is put together from besides the key: the batches, the conditions and the limit the configuration gave, and the labels the database registers and revokes.
/// </summary>
/// <param name="Batches">The batches the submissions open.</param>
/// <param name="Conditions">The conditions under which material may come in.</param>
/// <param name="MaxBytes">The most bytes a file may have.</param>
/// <param name="Registered">The labels the database registers.</param>
/// <param name="Revoked">The labels the database revoked.</param>
/// <param name="Replication">The criterion of the replicated content the configuration gave.</param>
/// <param name="Recognizer">The recognizer at the boundary that analyses every text before an item is written.</param>
internal sealed record SubmissionSettings(
    IngestionBatches Batches, OriginConditions Conditions, long MaxBytes, IReadOnlySet<string> Registered, IReadOnlySet<string> Revoked, ReplicationRule Replication,
    IRecognizer Recognizer);

using Core.Ingestion;
using Core.Ingestion.Entrance;
using Core.Persistence.Ingestion;

namespace Core.Host.Submissions;

/// <summary>What the service needs to take a file in and judge it: the barrier, the conference of the form and the conditions of origin the installation knows.</summary>
/// <param name="Gate">The barrier that reads the file, limits it and gives its digest.</param>
/// <param name="Conference">The conference that judges the material against the form declared.</param>
/// <param name="Conditions">The conditions the installation knows, the most restrictive first.</param>
internal sealed record IngestionIntake(SubmissionGate Gate, FormConference Conference, OriginConditions Conditions);

using Core.Ingestion;
using Core.Ingestion.Entrance;
using Core.Persistence.Content.Sealing;
using Core.Persistence.Ingestion;

namespace Core.Host.Submissions;

/// <summary>What the service needs to take a file in and judge it: the barrier, the conference of the form, the conditions of origin the installation knows and the sealing of the origin declared.</summary>
/// <param name="Gate">The barrier that reads the file, limits it and gives its digest.</param>
/// <param name="Conference">The conference that judges the material against the form declared.</param>
/// <param name="Conditions">The conditions the installation knows, the most restrictive first.</param>
/// <param name="Sealing">The path that seals the origin a submission declares, as the raw text of an item is sealed.</param>
internal sealed record SubmissionIntake(SubmissionGate Gate, FormConference Conference, OriginConditions Conditions, ProtectedContent Sealing);

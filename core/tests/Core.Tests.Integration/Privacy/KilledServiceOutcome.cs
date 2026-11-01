using Core.Host.Tests.Privacy;
using System.Net;

namespace Core.Tests.Integration.Privacy;

/// <summary>What the one run with the killed service showed: each answer, the interrupted reference, what the two logs held and every place that held the marker.</summary>
internal sealed record KilledServiceOutcome(
    HttpStatusCode Refused,
    HttpStatusCode Retried,
    HttpStatusCode Rejected,
    string Interrupted,
    string LogText,
    string Stdout,
    string Stderr,
    IReadOnlyList<LogLeak> Leaks);

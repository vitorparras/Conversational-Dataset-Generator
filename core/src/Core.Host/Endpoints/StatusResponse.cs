using Core.Host.Supervision;
using Core.Platform;
using Core.Platform.Startup;

namespace Core.Host.Endpoints;

/// <summary>
/// What the process says about itself: alive is always true when it answers; ready only when every check passed and,
/// with the http recognizer, the language service is available. With the fake no service is watched, and its state is
/// null. It also says when the checks ended and what each one concluded, and which kind of installation it is and how the last comparison of the server's audit log with the chain ended.
In the manual situation of the seal it also carries the warning that no seal was recorded or that the last is older than 7 days, and never the seal nor its date.
/// </summary>
internal sealed record StatusResponse(
    bool Alive,
    bool Ready,
    RecognizerKind Recognizer,
    ServiceState? LanguageService,
    DateTimeOffset CheckedAt,
    IReadOnlyList<CheckResult> Checks,
    Installation Installation,
    AuditComparisonStatus AuditComparison,
    string? SealWarning);

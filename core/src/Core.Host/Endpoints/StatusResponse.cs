using Core.Host.Supervision;
using Core.Platform.Startup;

namespace Core.Host.Endpoints;

/// <summary>
/// What the process says about itself: alive is always true when it answers; ready only when every check passed and,
/// with the http recognizer, the language service is available. With the fake no service is watched, and its state is null.
/// </summary>
internal sealed record StatusResponse(
    bool Alive, bool Ready, RecognizerKind Recognizer, ServiceState? LanguageService, IReadOnlyList<CheckResult> Checks);

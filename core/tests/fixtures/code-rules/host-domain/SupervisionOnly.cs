namespace Core.Host.Supervision;

internal sealed class RestartPolicy(int maximumRestarts)
{
    public bool AllowsRestart(int restartsSoFar) => restartsSoFar < maximumRestarts;

    public bool RefusesImpersonation { get; }
}

internal static partial class RestartLogMessages
{
    public static void RestartRequested(int attempt, string logMessage) => _ = (attempt, logMessage);
}

internal static class HealthEndpoint
{
    public static string Route => "/health";
}

internal enum RecognizerKind
{
    Fake,
    Http,
}

internal sealed class RecognizerSelection(IRecognizer recognizer)
{
    private readonly IRecognizer selected = recognizer;

    public IRecognizer Recognizer => selected;

    public static RecognizerKind Selected(RecognizerKind recognizerKind) => recognizerKind;
}

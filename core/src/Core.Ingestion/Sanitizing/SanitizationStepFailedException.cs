namespace Core.Ingestion.Sanitizing;

/// <summary>
/// A step of the sanitization failed, or returned edits that do not fit the text it received, so the text has no sanitized form. The exception names the step and carries the
/// failure as it came; neither its message nor this one holds a character of the text, which is never written where the failure is told.
/// </summary>
public sealed class SanitizationStepFailedException : InvalidOperationException
{
    /// <summary>The failure, with the words of whoever operates.</summary>
    public SanitizationStepFailedException()
        : base("Um passo da sanitização falhou; o texto ficou sem sanitizado.")
    {
    }

    /// <summary>The failure, with the words given.</summary>
    /// <param name="message">What to tell whoever operates.</param>
    public SanitizationStepFailedException(string? message)
        : base(message)
    {
    }

    /// <summary>The failure, with the one the step raised.</summary>
    /// <param name="message">What to tell whoever operates.</param>
    /// <param name="innerException">What the step raised.</param>
    public SanitizationStepFailedException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>The name of the step that failed.</summary>
    public string Step { get; init; } = string.Empty;

    /// <summary>The failure of the step named, with what it raised.</summary>
    /// <param name="step">The name of the step.</param>
    /// <param name="failure">What the step raised.</param>
    internal static SanitizationStepFailedException Of(string step, Exception failure) =>
        new($"O passo {step} da sanitização falhou; o texto ficou sem sanitizado.", failure) { Step = step };
}

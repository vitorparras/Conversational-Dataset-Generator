using Core.Ingestion.Deciding;

namespace Core.Ingestion.Sanitizing;

/// <summary>
/// The rerun of the sanitization of an item (issue 97): the steps in force run again over the raw text, never over the sanitized one, with the destinations decided when the item came
/// in, which are not decided again, and the text carries the version of the list in force (decision 0015). It is identical when it is, ordinal, the one the item holds.
/// </summary>
public sealed class SanitizationRerun
{
    private readonly Sanitizer sanitizer;

    /// <summary>The rerun by the steps in force.</summary>
    /// <param name="sanitizer">The sanitizer of the steps in force.</param>
    public SanitizationRerun(Sanitizer sanitizer)
    {
        this.sanitizer = sanitizer ?? throw new ArgumentNullException(nameof(sanitizer));
    }

    /// <summary>The version of the list of steps every rerun here produces.</summary>
    public string Version => sanitizer.Version;

    /// <summary>What the steps in force make of the raw text, and whether it is the text held.</summary>
    /// <param name="raw">The raw text, as it came in.</param>
    /// <param name="decided">Every occurrence of the raw text with the destination decided when it came in.</param>
    /// <param name="held">The sanitized text the item holds; null when it holds none.</param>
    /// <exception cref="ArgumentException">An occurrence falls outside the text.</exception>
    /// <exception cref="SanitizationStepFailedException">A step failed.</exception>
    public RerunText Rerun(string raw, IReadOnlyList<DecidedOccurrence> decided, string? held)
    {
        ArgumentNullException.ThrowIfNull(decided);
        var sanitized = sanitizer.Sanitize(raw, decided).Sanitized!;
        return new RerunText(sanitized, string.Equals(sanitized.Text, held, StringComparison.Ordinal));
    }
}

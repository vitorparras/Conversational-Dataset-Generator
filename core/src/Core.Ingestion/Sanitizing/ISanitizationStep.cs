namespace Core.Ingestion.Sanitizing;

/// <summary>
/// One step of the sanitization: it says who it is, what it alters and what it leaves, and answers with the edits it wants, never with the whole text, so that every change it makes
/// is a span the trail can translate. Its version changes whenever what it returns for the same input changes (decision 0015).
/// </summary>
public interface ISanitizationStep
{
    /// <summary>The name the version, the report and the startup check use.</summary>
    string Name { get; }

    /// <summary>The version of what the step does, which enters the version of the list.</summary>
    int Version { get; }

    /// <summary>What the step alters and what it leaves; null or blank is refused at startup.</summary>
    StepScope? Scope { get; }

    /// <summary>The spans to replace, ordered, disjoint and inside the text the step received.</summary>
    /// <param name="input">The text the earlier steps left, and the occurrences still in it.</param>
    IReadOnlyList<SanitizationEdit> Edits(StepInput input);
}

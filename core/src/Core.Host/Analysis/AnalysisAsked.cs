using Core.Platform.Logging;

namespace Core.Host.Analysis;

/// <summary>What is asked of the analysis route: a text, its language and, on a retry, the reference of the analysis it resumes.</summary>
/// <param name="Text">The text to analyse, before sanitization.</param>
/// <param name="Language">The language of the text.</param>
/// <param name="Resumes">The reference of the interrupted analysis this one resumes; null when it resumes none.</param>
internal sealed record AnalysisAsked([property: Material] string Text, string Language, Guid? Resumes = null)
{
    /// <summary>The language and the reference resumed; never the text.</summary>
    public override string ToString() => $"AnalysisAsked {{ Language = {Language}, Resumes = {Resumes} }}";
}

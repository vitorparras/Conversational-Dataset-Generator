using Core.Boundary.Text;
using Core.Ingestion.Sanitizing;

namespace Core.Host.Submissions;

/// <summary>The steps the installation declares for the sanitization, in their order, and the sanitizer that runs them over every text before its item is written (issue 87).</summary>
internal static class DeclaredSanitization
{
    /// <summary>The declared list: the application of the decided destinations alone, until the next phases add their steps.</summary>
    internal static readonly SanitizationSteps Steps = new([new DestinationsStep()]);

    /// <summary>The sanitizer over the declared list, whose version every item and the batch record.</summary>
    internal static readonly Sanitizer Sanitizer = new(Steps, new TextBoundaries());
}

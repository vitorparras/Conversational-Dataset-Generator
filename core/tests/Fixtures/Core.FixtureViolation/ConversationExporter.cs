using Core.FixtureModule;

namespace Core.FixtureViolation;

/// <summary>A domain rule where none may live, reaching another module's implementation.</summary>
public sealed class ConversationExporter
{
    /// <summary>A member named after an annotation category.</summary>
    public static int CountAnnotationCategories() => ModuleService.Answer();
}

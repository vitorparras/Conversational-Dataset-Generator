namespace Core.Tests.Properties.Harness;

/// <summary>How many cases a property runs and how long the whole run may take before it fails.</summary>
internal sealed record PropertyBudget(int Cases, TimeSpan Deadline)
{
    /// <summary>The budget every real property in this project runs under.</summary>
    public static PropertyBudget Declared { get; } = new(Cases: 200, Deadline: TimeSpan.FromSeconds(30));
}

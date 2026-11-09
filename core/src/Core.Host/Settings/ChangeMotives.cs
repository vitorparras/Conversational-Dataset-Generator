namespace Core.Host.Settings;

/// <summary>The closed list of motives of a change of configuration (decision 0053), as codes; the screen gives each its words, and a motive is never free text.</summary>
internal static class ChangeMotives
{
    /// <summary>The codes, in the order the screen offers them.</summary>
    internal static IReadOnlyList<string> All { get; } =
        new[] { "hardening", "operational_adjustment", "value_correction", "obligation", "incident_response" }.AsReadOnly();

    /// <summary>Whether the code is one of the list.</summary>
    internal static bool Has(string motive) => All.Contains(motive, StringComparer.Ordinal);
}

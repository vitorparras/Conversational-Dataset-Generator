namespace Core.Platform.Text;

/// <summary>
/// The unit every limit of a span counts in, from the boundary of the language service to what the core stores and, later, exports: the code point, as decision 0011 measures
/// the positions. It is declared here only, and what stores a limit writes it, so that a limit written in one unit is never read in another.
/// </summary>
public static class OffsetUnit
{
    /// <summary>The name of the unit as a stored limit declares it.</summary>
    public static string CodePoint => "code-point";
}

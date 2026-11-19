namespace Core.Ingestion.Sanitizing;

/// <summary>The text of each marker. Every marker is ASCII, so its length in UTF-16 units is its length in code points.</summary>
internal static class SanitizationMarkers
{
    /// <summary>The bracketed type written in place of the removed span.</summary>
    internal static string Text(SanitizationMarker marker) => marker switch
    {
        SanitizationMarker.Credential => "[credencial]",
        SanitizationMarker.Data => "[dado]",
        _ => throw new ArgumentOutOfRangeException(nameof(marker), marker, "The marker is not known."),
    };
}

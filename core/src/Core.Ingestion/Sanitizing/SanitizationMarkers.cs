namespace Core.Ingestion.Sanitizing;

/// <summary>The text of each marker. Every marker is ASCII, so its length in UTF-16 units is its length in code points.</summary>
internal static class SanitizationMarkers
{
    /// <summary>The bracketed type written in place of the removed span.</summary>
    internal static string Text(SanitizationMarker marker) => marker switch
    {
        SanitizationMarker.Credential => "[credencial]",
        SanitizationMarker.Data => "[dado]",
        SanitizationMarker.Document => "[documento]",
        SanitizationMarker.Contact => "[contato]",
        _ => throw new ArgumentOutOfRangeException(nameof(marker), marker, "The marker is not known."),
    };

    /// <summary>How many code points the marker takes in the sanitized text.</summary>
    internal static int Width(SanitizationMarker marker) => Text(marker).Length;
}

namespace Core.Ingestion;

/// <summary>Why a reader could not read the material; it names the failure and never carries a word of the content.</summary>
public enum UnreadableReason
{
    /// <summary>The file is damaged or is not of the format it says.</summary>
    Corrupted = 0,

    /// <summary>The file ends before the content does.</summary>
    Truncated = 1,

    /// <summary>The format is none the version reads.</summary>
    UnsupportedFormat = 2,

    /// <summary>Only a part of the content could be read; a part is never taken for the whole.</summary>
    OnlyPartlyRead = 3,
}

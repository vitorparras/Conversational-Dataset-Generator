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

    /// <summary>The content goes past a limit the reader declares: how deep it nests, how many units it has or how long a text is.</summary>
    OverLimits = 4,

    /// <summary>A member of the structure reads as an address or a path, which the format does not have and the reader does not follow.</summary>
    ExternalReference = 5,
}

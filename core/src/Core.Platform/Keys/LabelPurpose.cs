namespace Core.Platform.Keys;

/// <summary>What a key is for. The set is closed: another purpose enters only by a new decision.</summary>
public enum LabelPurpose
{
    /// <summary>The text before sanitization, and the sanitized text, which the same authorization covers.</summary>
    RawText = 0,

    /// <summary>The map that reverses a pseudonym to what it replaced.</summary>
    ReversalMap = 1,
}

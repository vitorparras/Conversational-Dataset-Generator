namespace Core.Ingestion.Sanitizing;

/// <summary>Why a material was not sanitized.</summary>
public enum SanitizationRefusal
{
    /// <summary>The occurrences of the material have no decided destination, so nothing says what may stay.</summary>
    Undecided = 0,
}

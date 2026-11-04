namespace Core.Persistence.Custody.Verification;

/// <summary>Why a text is not read as a seal; the first that applies, in this order, is the one told.</summary>
public enum SealProblem
{
    /// <summary>There is no text.</summary>
    Empty,

    /// <summary>The text does not start with the name and the version of the form this build reads.</summary>
    UnknownForm,

    /// <summary>A field is missing, or the check code is cut short.</summary>
    Incomplete,

    /// <summary>The moment is not written as year-month-day, the letter T, hours, minutes and seconds, and the letter Z.</summary>
    Moment,

    /// <summary>The count of events is not a whole number without sign or leading zeros.</summary>
    Count,

    /// <summary>The digest is not 64 lowercase hexadecimal digits.</summary>
    Digest,

    /// <summary>The check code is not the one of the rest of the text: it was altered or copied wrong.</summary>
    Check,
}

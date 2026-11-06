namespace Core.Ingestion.Entrance;

/// <summary>Why the gate turned a submission back; each is a reason that names the failure and never carries a word of the content or of the name.</summary>
public enum SubmissionRefusal
{
    /// <summary>The content passes the limit of bytes; the reading stopped at the first byte past it.</summary>
    TooLarge = 0,

    /// <summary>The version has no reader for the format declared.</summary>
    UnknownFormat = 1,

    /// <summary>The first bytes of the content do not confirm the format declared.</summary>
    FormatNotConfirmed = 2,

    /// <summary>The reader of the format could not read the content whole; the cause says why.</summary>
    Unreadable = 3,
}

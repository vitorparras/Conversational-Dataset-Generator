namespace Core.Platform.Keys;

/// <summary>Why a source gave no master key.</summary>
public enum MasterKeyFailureKind
{
    /// <summary>Nothing was provided.</summary>
    Absent = 0,

    /// <summary>Something was provided and could not be read as a key.</summary>
    Unreadable = 1,

    /// <summary>A key was read, but not of the size every master key has.</summary>
    WrongSize = 2,
}

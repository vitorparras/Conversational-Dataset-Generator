namespace Core.Platform.Versions;

/// <summary>Whether the working tree the executable was built from had changes not yet committed.</summary>
public enum WorkingTree
{
    /// <summary>The build could not ask; the state is absent, and neither clean nor dirty is assumed.</summary>
    Unknown = 0,

    /// <summary>Every change was committed: the executable matches the commit it names.</summary>
    Clean = 1,

    /// <summary>Some change was not committed: the executable matches no commit.</summary>
    Dirty = 2,
}

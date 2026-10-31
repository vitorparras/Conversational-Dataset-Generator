namespace Core.Persistence.People;

/// <summary>What the registry says about a person who asks to enter.</summary>
public enum EntryStatus
{
    /// <summary>The person is registered and active.</summary>
    Admitted,

    /// <summary>The person is registered and was deactivated.</summary>
    Deactivated,

    /// <summary>No person has the identifier.</summary>
    Unknown,
}

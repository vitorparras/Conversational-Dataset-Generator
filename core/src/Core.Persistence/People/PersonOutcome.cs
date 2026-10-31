namespace Core.Persistence.People;

/// <summary>How an operation over a person ended; every value but <see cref="Done" /> is a refusal that wrote nothing.</summary>
public enum PersonOutcome
{
    /// <summary>The person and the record of the change were written.</summary>
    Done,

    /// <summary>The name is empty or has white space at either end.</summary>
    NameInvalid,

    /// <summary>Another person already has the name.</summary>
    NameTaken,

    /// <summary>The function is not one of those the policy declares.</summary>
    FunctionNotAccepted,

    /// <summary>The person who acted is not registered.</summary>
    ActorUnknown,

    /// <summary>No person has the identifier.</summary>
    NotFound,

    /// <summary>The person is already deactivated.</summary>
    Deactivated,

    /// <summary>The person already holds the function asked for.</summary>
    FunctionUnchanged,
}

namespace Core.Boundary.Contracts;

/// <summary>Why no analysis came back; each one stops the material instead of passing it as clean.</summary>
public enum UnavailabilityReason
{
    /// <summary>The service did not answer within the time allowed.</summary>
    TimedOut = 0,

    /// <summary>The service could not be reached.</summary>
    ConnectionRefused = 1,

    /// <summary>The answer did not have the shape of the contract, and none of it is used.</summary>
    MalformedResponse = 2,

    /// <summary>The service speaks a version of the contract the core does not know.</summary>
    UnknownContractVersion = 3,
}

namespace Core.Host;

/// <summary>Where the installation keeps the seal of its chain of custody, which is what the check against the seal protects against; declared by every installation that serves.</summary>
internal enum SealSituation
{
    /// <summary>The seal is sent to a destination outside the control of whoever controls the database.</summary>
    External,

    /// <summary>The seal is sent to a destination inside the installation's own network, outside the database.</summary>
    Internal,

    /// <summary>The seal is not sent anywhere: whoever operates takes it from the chain and brings it back by hand.</summary>
    Manual,
}

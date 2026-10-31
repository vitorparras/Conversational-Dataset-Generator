namespace Core.Persistence.People;

/// <summary>What the check of a password found. The caller tells the person one answer only for every value but <see cref="Accepted" />; the values exist for whoever audits.</summary>
public enum PasswordOutcome
{
    /// <summary>The person is active and the password is theirs.</summary>
    Accepted,

    /// <summary>The person is active and the password is not theirs.</summary>
    Wrong,

    /// <summary>The person is registered and was deactivated; the password is not looked at.</summary>
    Deactivated,

    /// <summary>No person has the identifier.</summary>
    Unknown,

    /// <summary>The person is active and no password was set for them.</summary>
    NoPassword,
}

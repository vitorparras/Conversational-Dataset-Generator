namespace Core.Persistence.People;

/// <summary>What a notice tells, from a closed list: the text of each is the host's, and the database accepts no other code.</summary>
public enum NoticeCode
{
    /// <summary>A password was set again by someone else and ended sessions; the notice carries how many.</summary>
    PasswordReset,
    /// <summary>The last verification of the chain failed and the domains stopped accepting writes.</summary>
    DomainReadOnly,
}

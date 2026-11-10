namespace Core.Host.Settings;

/// <summary>Why the applier did not apply an approved change, or that it did; the name goes to the log of the server, which never carries a person.</summary>
internal enum ApplicationRefusal
{
    None,
    ChainBroken,
    SameAuthor,
    SubmitterNotAllowed,
    ApproverNotAllowed,
    KeyOnlyOnServer,
    ValueRefused,
    FileUnreadable,
}

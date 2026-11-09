namespace Core.Host.Settings;

/// <summary>What the refusals say beyond their names: whether they last, and the code that goes to the chain.</summary>
internal static class ApplicationRefusals
{
    /// <summary>Whether the refusal will not change by itself: a change refused for this reason is not applied ever, and the core says so in the chain. The others may pass.</summary>
    internal static bool IsDefinitive(this ApplicationRefusal refusal) => refusal switch
    {
        ApplicationRefusal.SameAuthor or ApplicationRefusal.SubmitterNotAllowed or ApplicationRefusal.ApproverNotAllowed
            or ApplicationRefusal.KeyOnlyOnServer or ApplicationRefusal.ValueRefused => true,
        ApplicationRefusal.None or ApplicationRefusal.ChainBroken or ApplicationRefusal.FileUnreadable => false,
        _ => throw new ArgumentOutOfRangeException(nameof(refusal), refusal, "The refusal is not one the applier knows."),
    };

    /// <summary>The code of a definitive refusal as the event of the chain carries it, which the screen turns into words.</summary>
    internal static string CodeOf(this ApplicationRefusal refusal) => refusal switch
    {
        ApplicationRefusal.SameAuthor => "same_author",
        ApplicationRefusal.SubmitterNotAllowed => "submitter_not_allowed",
        ApplicationRefusal.ApproverNotAllowed => "approver_not_allowed",
        ApplicationRefusal.KeyOnlyOnServer => "key_only_on_server",
        ApplicationRefusal.ValueRefused => "value_refused",
        ApplicationRefusal.None or ApplicationRefusal.ChainBroken or ApplicationRefusal.FileUnreadable
            => throw new ArgumentOutOfRangeException(nameof(refusal), refusal, "Only a definitive refusal has a code in the chain."),
        _ => throw new ArgumentOutOfRangeException(nameof(refusal), refusal, "The refusal is not one the applier knows."),
    };
}

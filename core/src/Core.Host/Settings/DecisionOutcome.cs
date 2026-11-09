namespace Core.Host.Settings;

/// <summary>How a decision on a change of configuration ended (decision 0053).</summary>
internal enum DecisionOutcome
{
    /// <summary>The decision went to the chain, pointing at the submission; the file stays as it is.</summary>
    Decided,

    /// <summary>There is no submission with this identifier.</summary>
    NotFound,

    /// <summary>Someone approved or refused it already.</summary>
    AlreadyDecided,

    /// <summary>The person who submitted it cannot decide it.</summary>
    OwnSubmission,

    /// <summary>It waited longer than the deadline and is decided no more.</summary>
    Expired,

    /// <summary>The person is not active with the technical administration.</summary>
    WithoutFunction,

    /// <summary>The chain did not take the decision, and nothing was written.</summary>
    NotRecorded,
}

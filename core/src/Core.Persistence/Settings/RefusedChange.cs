namespace Core.Persistence.Settings;

/// <summary>An approved change of configuration the core judged will never be applied (issue 748), as the screen reads it: which, why and the approval it points at; no value, no person.</summary>
public sealed record RefusedChange
{
    /// <summary>The submission.</summary>
    public required long Id { get; init; }

    /// <summary>The key, as <c>Section:Name</c>.</summary>
    public required string Key { get; init; }

    /// <summary>The code of the reason, in lower case and underscores, which the screen turns into words.</summary>
    public required string ReasonCode { get; init; }

    /// <summary>The sequence of the event of the approval.</summary>
    public required long ApprovalSequence { get; init; }

    /// <summary>The sequence of the event that recorded the refusal.</summary>
    public required long Sequence { get; init; }
}

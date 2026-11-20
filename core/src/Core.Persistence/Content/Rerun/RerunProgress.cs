namespace Core.Persistence.Content.Rerun;

/// <summary>Where a request stands: its version, when it was asked, its items by state and by result, the refusals and when the last item was concluded.</summary>
public sealed record RerunProgress
{
    /// <summary>The version the request reruns to.</summary>
    public required string TargetVersion { get; init; }

    /// <summary>When it was asked.</summary>
    public required DateTimeOffset RequestedAt { get; init; }

    /// <summary>The items waiting in the queue.</summary>
    public required long Queued { get; init; }

    /// <summary>The items a worker holds.</summary>
    public required long Held { get; init; }

    /// <summary>The items concluded with the text they held.</summary>
    public required long Identical { get; init; }

    /// <summary>The items whose text was replaced.</summary>
    public required long Replaced { get; init; }

    /// <summary>The items refused; <see cref="Refusals"/> says each.</summary>
    public required long Refused { get; init; }

    /// <summary>The items a request to another version closed before they were treated.</summary>
    public required long Superseded { get; init; }

    /// <summary>When the last item was concluded; none while none was.</summary>
    public DateTimeOffset? LastConcludedAt { get; init; }

    /// <summary>The items refused, each with its batch and the reason.</summary>
    public required IReadOnlyList<RerunItemRefusal> Refusals { get; init; }
}

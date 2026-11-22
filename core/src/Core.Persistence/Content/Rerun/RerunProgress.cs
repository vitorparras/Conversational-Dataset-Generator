namespace Core.Persistence.Content.Rerun;

/// <summary>Where a request stands: its version, when it was asked, its items by state and by result, when the last was concluded and the refusals.</summary>
/// <param name="TargetVersion">The version it reruns to.</param>
/// <param name="RequestedAt">When it was asked.</param>
/// <param name="Queued">Items waiting in the queue.</param>
/// <param name="Held">Items a worker holds.</param>
/// <param name="Identical">Items concluded with the text they held.</param>
/// <param name="Replaced">Items whose text was replaced.</param>
/// <param name="Refused">Items refused.</param>
/// <param name="Superseded">Items a request to another version closed before they were treated.</param>
/// <param name="LastConcludedAt">When the last item was concluded; none while none was.</param>
/// <param name="Refusals">Each item refused, with its batch and the reason.</param>
public sealed record RerunProgress(
    string TargetVersion, DateTimeOffset RequestedAt, long Queued, long Held, long Identical, long Replaced, long Refused, long Superseded, DateTimeOffset? LastConcludedAt,
    IReadOnlyList<RerunItemRefusal> Refusals);

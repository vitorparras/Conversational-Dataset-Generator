using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Persistence.Settings;
using Core.Platform.Configuration;

namespace Core.Host.Settings;

/// <summary>
/// Where the screen approves or refuses a change of configuration submitted (decision 0053). Only another person, active with the technical administration, decides a submission still pending
/// within the deadline the host started with, and once. The decision is an event of the chain that points at the submission: the desk never writes the file, which is the applier's.
/// </summary>
internal sealed class ChangeDecisionDesk
{
    private readonly ConfigurationChanges register;
    private readonly PersonRegistry registry;
    private readonly TimeProvider time;

    /// <summary>The desk over the configuration the host started with, the register of the submissions, the registry that says who has which function, and the clock.</summary>
    internal ChangeDecisionDesk(ConfigurationLoadResult inForce, ConfigurationChanges register, PersonRegistry registry, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(inForce);
        ArgumentNullException.ThrowIfNull(register);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(time);
        Deadline = ChangeDeadlineConfiguration.Of(inForce.Values);
        this.register = register;
        this.registry = registry;
        this.time = time;
    }

    /// <summary>How long a submission waits for a decision, as the host started with.</summary>
    internal TimeSpan Deadline { get; }

    /// <summary>Records the decision when nothing refuses it; nothing is written for a decision refused.</summary>
    internal Task<DecisionOutcome> DecideAsync(DecisionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return DecidingAsync(request, cancellationToken);
    }

    private async Task<DecisionOutcome> DecidingAsync(DecisionRequest request, CancellationToken cancellationToken)
    {
        var change = await register.SubmittedAsync(request.ChangeId, cancellationToken).ConfigureAwait(false);
        if (Refusal(change, request.DeciderId, time.GetUtcNow(), Deadline) is { } refused)
        {
            return refused;
        }

        if (await registry.ActiveFunctionOfAsync(request.DeciderId, cancellationToken).ConfigureAwait(false) != PersonFunctions.Administration)
        {
            return DecisionOutcome.WithoutFunction;
        }

        var recorded = await register.DecideAsync(request.DeciderId, change!, request.Approved, cancellationToken).ConfigureAwait(false);
        if (recorded.Outcome == CustodyOutcome.Appended)
        {
            return DecisionOutcome.Decided;
        }

        // A conflict is a decision taken meanwhile or a failure of serialization: only the first one is a decision already taken.
        return recorded.Outcome == CustodyOutcome.Conflict && await register.SubmittedAsync(request.ChangeId, cancellationToken).ConfigureAwait(false) is { Decided: true }
            ? DecisionOutcome.AlreadyDecided
            : DecisionOutcome.NotRecorded;
    }

    /// <summary>What refuses the decision before the function of the person is read, or null: the submission must exist, be pending, be of another person and be within the deadline.</summary>
    internal static DecisionOutcome? Refusal(SubmittedChange? change, long deciderId, DateTimeOffset now, TimeSpan deadline) => change switch
    {
        null => DecisionOutcome.NotFound,
        { Decided: true } => DecisionOutcome.AlreadyDecided,
        _ when change.AuthorId == deciderId => DecisionOutcome.OwnSubmission,
        _ when now - change.SubmittedAt > deadline => DecisionOutcome.Expired,
        _ => null,
    };
}

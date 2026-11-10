using Core.Persistence.Settings;

namespace Core.Host.Settings;

/// <summary>
/// Applies an approved change of configuration (decision 0053), the one step that writes the file. It applies only what the chain and the registry still stand behind: the chain closes, the two
/// authors are distinct people, active and with the technical administration, the key is one the screen changes, and the configuration that results is one the start takes. Whatever stops it
/// is told in the log of the server with the reason and the sequences of the two events, never with a name or an address of a person (requirement 021), and the file is left as it was.
/// </summary>
internal sealed class ConfigurationApplier
{
    private readonly ILoggerFactory loggers;

    /// <summary>The applier over the file at the path, which it writes, and the log it tells in.</summary>
    /// <param name="path">The settings file of the server.</param>
    /// <param name="time">The clock that stamps the copy of the file kept before a write.</param>
    /// <param name="loggers">Where the applier tells what it did or refused.</param>
    internal ConfigurationApplier(string path, TimeProvider time, ILoggerFactory loggers)
    {
        ArgumentNullException.ThrowIfNull(loggers);
        Target = new ConfigurationFile(path, time);
        this.loggers = loggers;
    }

    /// <summary>The file this applier writes, for whoever needs to read what it holds.</summary>
    internal ConfigurationFile Target { get; }

    /// <summary>Judges the change and, when nothing stops it, writes it into the file; <see cref="ApplicationRefusal.None" /> says it was written.</summary>
    /// <param name="change">The change approved, as the database holds it.</param>
    /// <param name="facts">What the chain and the registry say of it now.</param>
    /// <param name="inForce">The values the host would start with, which the new one is tried against.</param>
    internal ApplicationRefusal Apply(ApprovedChange change, ApplicationFacts facts, IReadOnlyDictionary<string, string> inForce)
    {
        ArgumentNullException.ThrowIfNull(change);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(inForce);
        var refusal = Judge(change, facts, inForce);
        if (refusal == ApplicationRefusal.None && !Target.TryChange(change.Key, change.Value))
        {
            refusal = ApplicationRefusal.FileUnreadable;
        }

        var log = loggers.CreateLogger(nameof(ConfigurationApplier));
        if (refusal == ApplicationRefusal.None)
        {
            log.ChangeApplied(change.Id, change.SubmissionSequence, change.ApprovalSequence);
        }
        else
        {
            log.ChangeNotApplied(change.Id, refusal, change.SubmissionSequence, change.ApprovalSequence);
        }

        return refusal;
    }

    private static ApplicationRefusal Judge(ApprovedChange change, ApplicationFacts facts, IReadOnlyDictionary<string, string> inForce)
    {
        if (!facts.ChainClosed)
        {
            return ApplicationRefusal.ChainBroken;
        }

        if (change.SubmitterId == change.ApproverId)
        {
            return ApplicationRefusal.SameAuthor;
        }

        if (!facts.SubmitterAllowed)
        {
            return ApplicationRefusal.SubmitterNotAllowed;
        }

        if (!facts.ApproverAllowed)
        {
            return ApplicationRefusal.ApproverNotAllowed;
        }

        if (!ChangeableKeys.Has(change.Key))
        {
            return ApplicationRefusal.KeyOnlyOnServer;
        }

        var problems = ConfigurationTrial.Problems(inForce, change.Key, change.Value);
        return problems.Any(problem => ChangeableKeys.Has(problem.Key)) ? ApplicationRefusal.ValueRefused : ApplicationRefusal.None;
    }
}

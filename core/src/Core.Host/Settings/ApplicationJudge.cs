using Core.Persistence.Settings;

namespace Core.Host.Settings;

/// <summary>
/// The judgment of an approved change of configuration (decision 0053), in one place for whoever applies it and whoever records that it will not be: the chain closes, the two authors are
/// distinct, active and with the technical administration, the key is on the list the screen changes and the configuration that results is one the start takes.
/// </summary>
internal static class ApplicationJudge
{
    /// <summary>What stops the change, or <see cref="ApplicationRefusal.None" /> when nothing does.</summary>
    /// <param name="change">The change approved.</param>
    /// <param name="facts">What the chain and the registry say of it now.</param>
    /// <param name="inForce">The values the new one is tried against.</param>
    internal static ApplicationRefusal Judge(ApprovedChange change, ApplicationFacts facts, IReadOnlyDictionary<string, string> inForce)
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

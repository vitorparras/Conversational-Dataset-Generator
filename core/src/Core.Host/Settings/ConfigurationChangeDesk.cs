using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Persistence.Settings;
using Core.Platform.Configuration;

namespace Core.Host.Settings;

/// <summary>
/// Where the screen submits a change of configuration (decision 0053). The motive must be of the list and the key one the screen changes; the configuration that would result is
/// judged by the schema and the rules of the start, against the values the host started with, and refused with the key and the value named when the start would refuse it.
/// Only then, for an active person with the technical administration, is the submission written with its event. The desk never writes the file: that is the applier's.
/// </summary>
internal sealed class ConfigurationChangeDesk
{
    private const string ShapeAccepted = "texto ASCII imprimível, de 1 a 256 caracteres";

    private readonly ConfigurationLoadResult inForce;
    private readonly ConfigurationChanges register;
    private readonly PersonRegistry registry;

    /// <summary>The desk over the configuration the host started with, the register of the submissions and the registry that says who has which function.</summary>
    internal ConfigurationChangeDesk(ConfigurationLoadResult inForce, ConfigurationChanges register, PersonRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(inForce);
        ArgumentNullException.ThrowIfNull(register);
        ArgumentNullException.ThrowIfNull(registry);
        this.inForce = inForce;
        this.register = register;
        this.registry = registry;
    }

    /// <summary>What refuses the change before anything is read from the database, or null when the change may be submitted: the same answer the start would give the result.</summary>
    internal ChangeAnswer? Judge(ChangeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!ChangeMotives.Has(request.Motive))
        {
            return new ChangeAnswer { Outcome = ChangeOutcome.MotiveUnknown };
        }

        if (!ChangeableKeys.Has(request.Key))
        {
            return new ChangeAnswer { Outcome = ChangeOutcome.OnlyOnServer };
        }

        if (inForce.View.Entries.Any(entry => entry.Key == request.Key && entry.Origin is ConfigurationOrigin.Environment or ConfigurationOrigin.Argument))
        {
            return new ChangeAnswer { Outcome = ChangeOutcome.OverriddenOnServer };
        }

        var problems = Problems(request);
        return problems.Count > 0 ? new ChangeAnswer { Outcome = ChangeOutcome.ValueRefused, Problems = problems } : null;
    }

    /// <summary>Judges the change and, when nothing refuses it, writes the submission with its event; nothing is written for a change refused.</summary>
    internal async Task<ChangeAnswer> SubmitAsync(ChangeRequest request, CancellationToken cancellationToken)
    {
        if (Judge(request) is { } refused)
        {
            return refused;
        }

        if (await registry.ActiveFunctionOfAsync(request.AuthorId, cancellationToken).ConfigureAwait(false) != PersonFunctions.Administration)
        {
            return new ChangeAnswer { Outcome = ChangeOutcome.WithoutFunction };
        }

        var written = await register.SubmitAsync(Submission(request), cancellationToken).ConfigureAwait(false);
        return written.Recorded.Outcome == CustodyOutcome.Appended
            ? new ChangeAnswer { Outcome = ChangeOutcome.Recorded, ChangeId = written.ChangeId, Sequence = written.Recorded.Sequence }
            : new ChangeAnswer { Outcome = ChangeOutcome.NotRecorded };
    }

    private static ConfigurationSubmission Submission(ChangeRequest request) =>
        new() { Key = request.Key, Value = request.Value, Motive = request.Motive, AuthorId = request.AuthorId };

    private IReadOnlyList<ConfigurationProblem> Problems(ChangeRequest request)
    {
        if (!Submission(request).HasShape)
        {
            return new[] { new ConfigurationProblem(ConfigurationProblemKind.ValueNotAccepted, request.Key, request.Value, new[] { ShapeAccepted }.AsReadOnly()) }.AsReadOnly();
        }

        var trial = ClosedConfiguration.Load(HostConfiguration.Schema(migrating: false, comparing: false), new TrialSource(inForce.Values, request.Key, request.Value));
        return HostRefusals.OfService(trial);
    }
}

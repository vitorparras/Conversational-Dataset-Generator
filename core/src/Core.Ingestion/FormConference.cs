using Core.Boundary.Contracts;

namespace Core.Ingestion;

/// <summary>
/// The conference of the form declared against the content read, the one barrier between a badly read file and a dataset with the wrong authorship. What was not read whole is refused,
/// and what was read goes to the rule of the form declared; neither the name of the file nor the place of the text on the page is ever asked.
/// </summary>
public sealed class FormConference
{
    private readonly Dictionary<MaterialForm, IFormRule> rules;

    /// <summary>The conference over one rule for each of the four forms.</summary>
    /// <param name="rules">Exactly one rule for each form.</param>
    internal FormConference(IEnumerable<IFormRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var byForm = new Dictionary<MaterialForm, IFormRule>();
        foreach (var rule in rules)
        {
            ArgumentNullException.ThrowIfNull(rule);
            if (!byForm.TryAdd(rule.Form, rule))
            {
                throw new ArgumentException($"There are two rules for the form {rule.Form}.", nameof(rules));
            }
        }

        if (byForm.Count != Enum.GetValues<MaterialForm>().Length)
        {
            throw new ArgumentException("There is not one rule for each of the four forms.", nameof(rules));
        }

        this.rules = byForm;
    }

    /// <summary>The conference with the minimal rule of each form.</summary>
    /// <param name="boundaries">The boundary that tells where a text can be cut, which the labeled sequence asks.</param>
    public static FormConference Minimal(ITextBoundaries boundaries)
    {
        ArgumentNullException.ThrowIfNull(boundaries);
        var minimal = new IFormRule[Enum.GetValues<MaterialForm>().Length];
        minimal[(int)MaterialForm.Conversation] = new ConversationRule();
        minimal[(int)MaterialForm.Comment] = new CommentRule();
        minimal[(int)MaterialForm.Document] = new DocumentRule();
        minimal[(int)MaterialForm.LabeledSequence] = new LabeledSequenceRule(boundaries);
        return new FormConference(minimal);
    }

    /// <summary>Confers what the reader gave against the form declared.</summary>
    /// <param name="declared">The form the one who submitted declared.</param>
    /// <param name="outcome">What the reader gave back.</param>
    public ConferenceResult Confer(MaterialForm declared, ReadOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        if (!Enum.IsDefined(declared))
        {
            throw new ArgumentOutOfRangeException(nameof(declared), declared, "The form is not one of the four.");
        }

        if (outcome.Material is not { } read)
        {
            return ConferenceResult.Refuse(Refusal.Whole(declared, RefusalCode.Unreadable, outcome.Reason));
        }

        return read.Units.Count == 0
            ? ConferenceResult.Refuse(Refusal.Whole(declared, RefusalCode.NoUnits, null))
            : rules[declared].Confer(read);
    }
}

namespace Core.Ingestion;

/// <summary>The rule of one form: it confers what was read against what its form needs and gives the material of that form or the refusal. The rules of each form go deeper through here.</summary>
internal interface IFormRule
{
    /// <summary>The form this rule confers.</summary>
    MaterialForm Form { get; }

    /// <summary>Confers the material read, which has at least one unit, against the form of the rule.</summary>
    /// <param name="read">What the reader read, whole.</param>
    ConferenceResult Confer(ReadMaterial read);
}

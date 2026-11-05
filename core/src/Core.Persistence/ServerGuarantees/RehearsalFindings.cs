namespace Core.Persistence.ServerGuarantees;

/// <summary>
/// What a rehearsal of a restore found that did not hold, as a closed list of bits: the mask is the item of the event that records it. The first five share the value of the guarantee of
/// <see cref="GuaranteeKinds" /> they name, with names short enough for the 64 characters of a reference.
/// The sample of sealed records is not among them: opening it would read content, and a rehearsal reads none.
/// </summary>
[Flags]
public enum RehearsalFindings : long
{
    /// <summary>Everything the rehearsal conferred held.</summary>
    None = 0,
    /// <summary>The role of the application is not what it must be: a superuser, bypassing row security, or owner of something.</summary>
    Privilege = 1,
    /// <summary>A table keeps no row policy, or keeps it without FORCE.</summary>
    Policy = 2,
    /// <summary>The write permission granted by column is voided by one on the whole table.</summary>
    Revoke = 4,
    /// <summary>The triggers that keep an event of the chain from being changed, removed or emptied are missing or not enabled.</summary>
    Immutable = 8,
    /// <summary>A column of the content schema is neither sealed with the mark of its purpose nor listed as stored in clear.</summary>
    Columns = 16,
    /// <summary>A link of the chain does not hold.</summary>
    Chain = 64,
    /// <summary>The chain does not have the event the seal noted outside counts, or has it with another digest.</summary>
    Seal = 128,
}

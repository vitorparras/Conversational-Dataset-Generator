namespace Core.Persistence.ServerGuarantees;

/// <summary>The guarantees the conference of the server reads, as a closed list of bits: the mask of the ones missing is the item of the event that records it (migration 0017).</summary>
[Flags]
public enum GuaranteeKinds : long
{
    /// <summary>Every guarantee held.</summary>
    None = 0,
    /// <summary>The role of the application is not a superuser, does not bypass row security and owns no database, schema or table.</summary>
    Privilege = 1,
    /// <summary>Every table with a row policy applies it with FORCE, and the content schema keeps the domain policy.</summary>
    RowPolicy = 2,
    /// <summary>The write permission granted by column is not voided by one on the whole table.</summary>
    ColumnRevoke = 4,
    /// <summary>The triggers that keep an event of the chain from being changed, removed or emptied are on the table, enabled for every session.</summary>
    Immutability = 8,
    /// <summary>Every column of the content schema is sealed with the mark of its purpose or listed as stored in clear.</summary>
    ProtectedColumns = 16,
    /// <summary>The sample of sealed records opens under the label they declare.</summary>
    EncryptedSample = 32,
}

namespace Core.Persistence.Custody;

/// <summary>The reasons a custody event may give; the list lives here only, so that no free text reaches the chain.</summary>
public enum CustodyReason
{
    /// <summary>An act of whoever administers, done on purpose.</summary>
    AdministrativeAct,

    /// <summary>The correction of what an earlier event recorded.</summary>
    CorrectionOfRecord,
}

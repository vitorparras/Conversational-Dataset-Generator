namespace Core.Host;

/// <summary>What the last periodic comparison of the server's audit log with the chain found.</summary>
internal enum AuditComparisonResult
{
    /// <summary>None has run yet: the first waits for its interval.</summary>
    Pending,

    /// <summary>The two layers agree, or differ only as already known.</summary>
    Agreed,

    /// <summary>There is a difference that is new.</summary>
    Different,

    /// <summary>The comparison could not run, because the log or the database could not be read.</summary>
    Failed,
}

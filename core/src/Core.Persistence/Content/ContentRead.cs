using Core.Persistence.Custody;

namespace Core.Persistence.Content;

/// <summary>A reading of one protected column of one row, by someone.</summary>
/// <param name="Author">Who reads; the event of the reading names them.</param>
/// <param name="Column">The protected column.</param>
/// <param name="DomainId">The domain whose row is read.</param>
/// <param name="RowId">The identifier of the row in the table of the column.</param>
public sealed record ContentRead(CustodyAuthor Author, ProtectedColumn Column, int DomainId, long RowId);

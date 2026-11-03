using Core.Persistence.ServerGuarantees;

namespace Core.Host.Endpoints;

/// <summary>
/// The answer to a listing of the server's audit log: the interval asked, in UTC, the lines the server wrote in it, oldest first, and the names of the files
/// whose last line the server's fall cut short, which the listing leaves out.
/// </summary>
internal sealed record AuditListed(DateTimeOffset From, DateTimeOffset To, IReadOnlyList<ServerAuditEntry> Entries, IReadOnlyList<string> CutOffFiles);

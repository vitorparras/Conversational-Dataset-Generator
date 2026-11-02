using Core.Persistence.ServerGuarantees;

namespace Core.Host.Endpoints;

/// <summary>The answer to a listing of the server's audit log: the interval asked, in UTC, and the lines the server wrote in it, oldest first.</summary>
internal sealed record AuditListed(DateTimeOffset From, DateTimeOffset To, IReadOnlyList<ServerAuditEntry> Entries);

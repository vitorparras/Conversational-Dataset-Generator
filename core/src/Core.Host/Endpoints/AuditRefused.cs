namespace Core.Host.Endpoints;

/// <summary>The answer to a listing whose interval is not two instants in order, with what is accepted.</summary>
internal sealed record AuditRefused(string Accepted);

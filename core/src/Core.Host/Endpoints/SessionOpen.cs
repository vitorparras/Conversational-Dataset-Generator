namespace Core.Host.Endpoints;

/// <summary>Metadata of a route that answers with no session: the entry, which makes one, and the status, which says whether the host is alive. Every other route is behind the gate.</summary>
/// <param name="Reason">Why the route has no session to ask, for whoever reads the list of exceptions.</param>
internal sealed record SessionOpen(string Reason = "answers before there is a session");

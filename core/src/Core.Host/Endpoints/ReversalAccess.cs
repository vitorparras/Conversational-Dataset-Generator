using Core.Persistence.Content.Reversal;
using Core.Platform.Keys;

namespace Core.Host.Endpoints;

/// <summary>The requests of a reversal, their reading and their revelation over one reading of the key, which is let go of with them.</summary>
internal sealed record ReversalAccess(ReversalRequests Requests, ReversalRequestReader Reader, ReversalRevelations Revelations, MasterKeyReading Reading) : IDisposable
{
    public void Dispose() => Reading.Dispose();
}

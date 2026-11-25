using Core.Persistence.Content.Reversal;
using Core.Platform.Keys;

namespace Core.Host.Endpoints;

/// <summary>The requests of a reversal and their reading over one reading of the key, which is let go of with them.</summary>
internal sealed record ReversalAccess(ReversalRequests Requests, ReversalRequestReader Reader, MasterKeyReading Reading) : IDisposable
{
    public void Dispose() => Reading.Dispose();
}

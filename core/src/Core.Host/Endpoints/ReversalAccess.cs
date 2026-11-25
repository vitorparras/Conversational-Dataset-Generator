using Core.Persistence.Content.Reversal;
using Core.Platform.Keys;

namespace Core.Host.Endpoints;

/// <summary>The requests of a reversal and their reading over one reading of the key, which is let go of with them.</summary>
internal sealed class ReversalAccess(ReversalRequests requests, ReversalRequestReader reader, MasterKeyReading reading) : IDisposable
{
    internal ReversalRequests Requests { get; } = requests;

    internal ReversalRequestReader Reader { get; } = reader;

    public void Dispose() => reading.Dispose();
}

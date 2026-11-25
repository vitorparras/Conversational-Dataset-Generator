using Core.Persistence.Content.Reversal;
using Core.Platform.Keys;

namespace Core.Host.Endpoints;

/// <summary>The requests of a reversal and their reading over one reading of the key, which is let go of with them.</summary>
internal sealed class ReversalAccess : IDisposable
{
    private readonly MasterKeyReading reading;

    internal ReversalAccess(ReversalRequests requests, ReversalRequestReader reader, MasterKeyReading reading) => (Requests, Reader, this.reading) = (requests, reader, reading);

    internal ReversalRequests Requests { get; }

    internal ReversalRequestReader Reader { get; }

    public void Dispose() => reading.Dispose();
}

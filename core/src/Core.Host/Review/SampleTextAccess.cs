using Core.Persistence.Ingestion;

namespace Core.Host.Review;

/// <summary>
/// The reader of the text of the sample over the master key a reading holds, which the screen lets go of when it ends; with no reader the key could not be read.
/// </summary>
internal sealed record SampleTextAccess(SampleTextReader? Reader, IDisposable? Key) : IDisposable
{
    public void Dispose() => Key?.Dispose();
}

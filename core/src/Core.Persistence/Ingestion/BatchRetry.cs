using System.Security.Cryptography;

namespace Core.Persistence.Ingestion;

/// <summary>
/// The repeating of a write to a batch that met another writer at the same link of the chain: at most <see cref="Attempts" /> attempts in all, with a short random wait between them.
/// Only the refusal for a collision is repeated; any other answer, written or refused, is the answer of the first attempt that gave it.
/// </summary>
internal static class BatchRetry
{
    /// <summary>The attempts in all, the first included.</summary>
    internal const int Attempts = 3;

    private const int ShortestWait = 10;
    private const int LongestWait = 60;

    /// <summary>Makes the attempt, and makes it again while the chain refuses it for a collision and there are attempts left; the last answer is the one that comes back.</summary>
    /// <param name="attempt">The write, which opens its own transaction each time it runs.</param>
    /// <param name="cancellationToken">Cancels the waiting between the attempts.</param>
    internal static async Task<BatchResult> RunAsync(Func<Task<BatchResult>> attempt, CancellationToken cancellationToken)
    {
        var result = await attempt().ConfigureAwait(false);
        for (var made = 1; made < Attempts && result.Collided; made++)
        {
            await Task.Delay(RandomNumberGenerator.GetInt32(ShortestWait, LongestWait), cancellationToken).ConfigureAwait(false);
            result = await attempt().ConfigureAwait(false);
        }

        return result;
    }
}

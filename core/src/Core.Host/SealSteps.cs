using Core.Boundary.Http;
using Core.Persistence.Custody.Verification;

namespace Core.Host;

/// <summary>What sending the seal does, one step at a time: make the seal of the tip, send its text, record the seal that was sent, and tell when the last one was recorded.</summary>
/// <param name="Make">Seals the chain as it stands, recording nothing.</param>
/// <param name="Send">Sends the text of a seal to the destination declared.</param>
/// <param name="Record">Records the seal that was sent, as the system.</param>
/// <param name="Last">When the chain last recorded a seal; null when it never did.</param>
internal sealed record SealSteps(
    Func<CancellationToken, Task<ChainSeal>> Make,
    Func<string, CancellationToken, Task<SealDeliveryResult>> Send,
    Func<ChainSeal, CancellationToken, Task<SealRecording>> Record,
    Func<CancellationToken, Task<DateTimeOffset?>> Last);

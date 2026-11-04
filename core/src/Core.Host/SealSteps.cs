using Core.Boundary.Http;
using Core.Persistence.Custody.Verification;

namespace Core.Host;

internal sealed record SealSteps(
    Func<CancellationToken, Task<ChainSeal>> Make,
    Func<string, CancellationToken, Task<SealDeliveryResult>> Send,
    Func<ChainSeal, CancellationToken, Task<SealRecording>> Record,
    Func<CancellationToken, Task<DateTimeOffset?>> Last);

using Core.Persistence.Custody.Verification;

namespace Core.Host.Endpoints;

/// <summary>What asking for a verification came to: it was done (with the verdict), it was refused for coming too soon (with the seconds to wait), or it did not reach its end (both null).</summary>
/// <param name="Done">The verification and the verdict it recorded; null when it was refused or did not reach its end.</param>
/// <param name="WaitSeconds">The seconds to wait before asking again; null unless the session asked too soon.</param>
internal sealed record AskedVerification(ChainVerification? Done, int? WaitSeconds);

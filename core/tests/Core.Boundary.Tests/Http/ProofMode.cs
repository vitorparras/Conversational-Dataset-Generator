namespace Core.Boundary.Tests.Http;

/// <summary>What the fake service answers when the core asks it to prove it holds the secret.</summary>
internal enum ProofMode
{
    /// <summary>The HMAC of the nonce keyed by the secret.</summary>
    Right,

    /// <summary>The HMAC of another nonce, as a process that does not hold the secret would have to guess.</summary>
    Wrong,

    /// <summary>An answer with no proof, as any process that is not the service would give.</summary>
    Missing,
}

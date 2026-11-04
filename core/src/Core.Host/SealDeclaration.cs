namespace Core.Host;

/// <summary>
/// What the installation declared about its seal: the situation, and the destination when the situation has one. The system cannot see the destination, so it tells that it is a declaration.
/// </summary>
/// <param name="Situation">Where the seal is kept.</param>
/// <param name="Destination">The https address the seal is sent to; null when the situation is manual.</param>
internal sealed record SealDeclaration(SealSituation Situation, Uri? Destination);

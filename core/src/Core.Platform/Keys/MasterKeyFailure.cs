namespace Core.Platform.Keys;

/// <summary>Why no key was read and what was found instead, in words for the operator; the key's value is never among them.</summary>
public sealed record MasterKeyFailure(MasterKeyFailureKind Kind, string Found);

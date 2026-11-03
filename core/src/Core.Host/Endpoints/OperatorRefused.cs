namespace Core.Host.Endpoints;

/// <summary>The answer to an operation over a person that was refused: the field at fault when one is, and the reason in words.</summary>
internal sealed record OperatorRefused(string? Field, string Reason);

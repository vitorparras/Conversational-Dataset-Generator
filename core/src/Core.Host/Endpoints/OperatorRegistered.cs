namespace Core.Host.Endpoints;

/// <summary>
/// How the registration of a person came to an end, for the route of JSON and for the screen alike: the identifier the registry gave when it was made, or the refusal with the field at fault
/// and the status the route answers with. The first password is in neither.
/// </summary>
/// <param name="Id">The identifier the registry gave; null when the person was not made, or was made with no password.</param>
/// <param name="Refusal">The field at fault, when one is, and the reason in words; null when the person was made.</param>
/// <param name="Status">The status the route answers with: 201 when made.</param>
internal sealed record OperatorRegistered(long? Id, OperatorRefused? Refusal, int Status);

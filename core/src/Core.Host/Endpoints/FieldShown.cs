namespace Core.Host.Endpoints;

/// <summary>A field of the request that is missing or invalid, by name and by code, never by value.</summary>
/// <param name="Field">The field.</param>
/// <param name="Code">Why: <c>missing</c>, <c>invalid</c>, <c>conflict</c>, <c>too_long</c> or <c>unknown</c>.</param>
internal sealed record FieldShown(string Field, string Code);

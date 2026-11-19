namespace Core.Ingestion.Sanitizing;

/// <summary>Which step ran, by name and version, in the order the list declares.</summary>
/// <param name="Name">The name of the step.</param>
/// <param name="Version">The version of the step.</param>
public sealed record StepIdentity(string Name, int Version);

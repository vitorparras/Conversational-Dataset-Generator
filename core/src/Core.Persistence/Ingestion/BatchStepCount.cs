namespace Core.Persistence.Ingestion;

/// <summary>What one step of the sanitization did to the items of a batch: the name of the step, how many edits it made and how many items it changed; never a position of the trail.</summary>
/// <param name="Name">The name of the step, never a word of the material.</param>
/// <param name="Edits">How many edits the step made over the items of the batch.</param>
/// <param name="Items">How many items the step changed.</param>
public sealed record BatchStepCount(string Name, long Edits, long Items);

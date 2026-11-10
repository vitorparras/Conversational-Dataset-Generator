namespace Core.Persistence.Settings;

/// <summary>A submission still waiting for a decision, with the name of who submitted it, as the screen lists it.</summary>
/// <param name="Change">The submission.</param>
/// <param name="AuthorName">The name of the person who submitted it.</param>
public sealed record PendingChange(SubmittedChange Change, string AuthorName);

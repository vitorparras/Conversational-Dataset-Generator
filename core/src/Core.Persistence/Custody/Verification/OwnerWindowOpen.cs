namespace Core.Persistence.Custody.Verification;

/// <summary>A window of the owner credential that is still open, as the chain records it; it carries no content of any event.</summary>
/// <param name="OpenedSequence">The sequence of the event that opened the window.</param>
/// <param name="OpenedAt">The moment the server recorded the opening.</param>
/// <param name="From">The version of the schema the migration left.</param>
/// <param name="To">The version of the schema the migration reached.</param>
/// <param name="TipSequence">The sequence of the last event of the chain when the window opened; zero when there was none.</param>
/// <param name="AfterTheStep">Whether the opening was written after the step that creates it, in a database older than that step.</param>
/// <param name="ClosingFailed">Whether the last closing tried was recorded as failed.</param>
public sealed record OwnerWindowOpen(long OpenedSequence, DateTimeOffset OpenedAt, int From, int To, long TipSequence, bool AfterTheStep, bool ClosingFailed);

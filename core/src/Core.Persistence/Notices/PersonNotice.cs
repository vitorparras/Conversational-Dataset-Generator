namespace Core.Persistence.Notices;

/// <summary>A notice of a person that was not yet read, as the list shows it.</summary>
/// <param name="Id">The identifier of the notice.</param>
/// <param name="Code">What the notice tells.</param>
/// <param name="SessionsEnded">How many sessions the act ended; null for a notice that carries no number.</param>
/// <param name="CreatedAt">When the notice was made.</param>
public sealed record PersonNotice(long Id, NoticeCode Code, int? SessionsEnded, DateTimeOffset CreatedAt);

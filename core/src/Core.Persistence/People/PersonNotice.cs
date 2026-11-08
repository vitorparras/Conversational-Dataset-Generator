namespace Core.Persistence.People;

/// <summary>A notice of a person that was not yet read, as the list shows it.</summary>
public sealed record PersonNotice(long Id, NoticeCode Code, int? SessionsEnded, DateTimeOffset CreatedAt);

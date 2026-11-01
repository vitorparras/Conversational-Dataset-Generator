namespace Core.Persistence.ServerGuarantees;

/// <summary>
/// What the server's audit log says it saw one session do: when, as which role, in which database, the class of the statement, its command and
/// the object it touched. The text of the statement is not kept, since the log is read to be compared and not to be read back.
/// </summary>
/// <param name="At">When the server wrote the line, in UTC.</param>
/// <param name="Role">The role the session ran as.</param>
/// <param name="Database">The database the session was connected to.</param>
/// <param name="Class">The pgaudit class of the statement, such as READ or WRITE.</param>
/// <param name="Command">The command, such as INSERT or TRUNCATE TABLE.</param>
/// <param name="ObjectType">The kind of object the statement touched, such as TABLE; null when the line names none.</param>
/// <param name="ObjectName">The object, with its schema, such as public.person; null when the line names none.</param>
public sealed record ServerAuditEntry(DateTimeOffset At, string Role, string Database, string Class, string Command, string? ObjectType, string? ObjectName);

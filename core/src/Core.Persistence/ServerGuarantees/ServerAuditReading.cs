namespace Core.Persistence.ServerGuarantees;

/// <summary>What a reading of the server's audit log found: the lines in the interval, oldest first, and the files that end in a line the server's fall cut short.</summary>
/// <param name="Entries">The lines written in the interval.</param>
/// <param name="CutOffFiles">The names of the files, other than the newest, whose last line has no line break: the server fell while writing it, so the line is neither an entry nor an error.</param>
public sealed record ServerAuditReading(IReadOnlyList<ServerAuditEntry> Entries, IReadOnlyList<string> CutOffFiles);

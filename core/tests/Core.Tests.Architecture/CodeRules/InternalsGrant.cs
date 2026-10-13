namespace Core.Tests.Architecture.CodeRules;

/// <summary>One assembly a project file lets see its internals, and the line that grants it.</summary>
internal sealed record InternalsGrant(string Target, int Line);

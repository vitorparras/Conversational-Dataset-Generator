namespace Core.Host.Endpoints;

/// <summary>Why the text of a seal was not read, answered with 400 before anything is verified: the problem by name and in words.</summary>
/// <param name="Field">The field of the request at fault.</param>
/// <param name="Problem">The kind of the problem, as the seal reading names it.</param>
/// <param name="Reason">The reason in words, for whoever operates.</param>
internal sealed record SealRefused(string Field, string Problem, string Reason);

namespace Core.Boundary.Tests.Http;

/// <summary>A request as the fake service received it.</summary>
internal sealed record SeenRequest(string Method, string Path, string? Secret, string Body);

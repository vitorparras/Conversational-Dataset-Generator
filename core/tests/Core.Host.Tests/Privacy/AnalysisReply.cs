using System.Net;

namespace Core.Host.Tests.Privacy;

/// <summary>What the analysis route answered: the status, the opaque reference of the analysis, and the occurrences when it concluded.</summary>
internal sealed record AnalysisReply(HttpStatusCode Status, string? Reference, int? Occurrences);

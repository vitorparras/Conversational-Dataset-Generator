using Core.Host.Submissions;

namespace Core.Host.Endpoints;

/// <summary>What the body of a submission gave up to its file: the request, with the file as the stream it is, and whether a file part was there at all.</summary>
/// <param name="Request">The request, whose content has not been read.</param>
/// <param name="HasFile">Whether the body had a file part; without one the content is empty.</param>
internal sealed record SubmissionRead(SubmissionRequest Request, bool HasFile);

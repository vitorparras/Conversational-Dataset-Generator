using Core.Ingestion;
using Core.Ingestion.Entrance;
using Core.Persistence.Ingestion;

namespace Core.Host.Submissions;

/// <summary>The origin a submission declares: what is wrong with it as the request brings it, and the declaration sealed for the domain.</summary>
internal static class SubmissionOrigin
{
    /// <summary>The origin is required, and saying it is unknown is an answer of its own that excludes a text; the text has a size.</summary>
    /// <param name="request">The request, with the text of the origin or the choice of saying it is unknown.</param>
    internal static string? Problem(SubmissionRequest request)
    {
        var text = request.Origin?.Trim();
        return (string.IsNullOrEmpty(text), request.OriginUnknown) switch
        {
            (true, false) => "missing",
            (false, true) => "conflict",
            (false, false) when text!.Length > OriginDeclaration.MostCharacters => "too_long",
            _ => null,
        };
    }

    /// <summary>
    /// The origin of the request, sealed for the domain; null when the domain has no key for it, which is a domain that cannot take the material;
    /// with no key at all the declaration stands as declared and its text is not kept.
    /// </summary>
    /// <param name="intake">What takes the file in, with the path that seals protected content.</param>
    /// <param name="request">The request, already valid.</param>
    internal static OriginDeclaration? Declaration(SubmissionIntake intake, SubmissionRequest request)
    {
        if (request.OriginUnknown)
        {
            return OriginDeclaration.Unknown;
        }

        if (intake.Sealing is null)
        {
            return OriginDeclaration.DeclaredNotKept;
        }

        try
        {
            return OriginDeclaration.Declared(intake.Sealing, request.DomainId, request.Origin!.Trim());
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// The opening of the batch that keeps the submission: its receipt, its origin, the form it declared and the form its format carries;
    /// null when the origin cannot be declared, which is a domain that cannot take the material.
    /// </summary>
    /// <param name="intake">What takes the file in, with the path that seals protected content.</param>
    /// <param name="request">The request, already valid.</param>
    /// <param name="received">What the barrier answered, whose receipt the batch keeps.</param>
    /// <param name="warning">What the name received carries that is worth a warning; none when nothing is.</param>
    internal static BatchOpening? Opening(SubmissionIntake intake, SubmissionRequest request, SubmissionResult received, string? warning) =>
        Declaration(intake, request) is { } origin
            ? new BatchOpening(request.DomainId, request.AuthorId, request.Condition, new SubmissionOpening(ReceiptOf(received, warning), origin))
            {
                DeclaredForm = LossCodes.Code(request.DeclaredForm),
                SourceForm = SourceOf(intake, request),
            }
            : null;

    private static SubmissionReceipt ReceiptOf(SubmissionResult received, string? warning) => new(received.Id.ToString(), received.BytesRead, received.Digest, warning);

    /// <summary>The code of the form the declared format carries; none when the version has no reader of it.</summary>
    private static string? SourceOf(SubmissionIntake intake, SubmissionRequest request) =>
        intake.Gate.FormOf(request.SourceFormat) is { } source ? LossCodes.Code(source) : null;

    /// <summary>The warning of a name that came with a path: the separators of a folder, a step up, or a control character; none when the name is plain or absent.</summary>
    /// <param name="name">The name the file was received with.</param>
    internal static string? WarningOf(string? name) =>
        name is not null && HasPath(name) ? "name_with_path" : null;

    private static bool HasPath(string name) => HasSeparator(name) || name.Contains("..", StringComparison.Ordinal) || name.Any(char.IsControl);

    private static bool HasSeparator(string name) => name.Contains('/', StringComparison.Ordinal) || name.Contains('\\', StringComparison.Ordinal);
}

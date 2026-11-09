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
}

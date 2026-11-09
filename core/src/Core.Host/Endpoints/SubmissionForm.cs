using System.Globalization;
using System.Text;
using Core.Host.Submissions;
using Core.Ingestion;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace Core.Host.Endpoints;

/// <summary>
/// The body of a submission read as a stream, part by part: the fields come first and the file last, and the file is handed on as the stream it is, so that a file past the limit is turned back
/// before a byte of it is written anywhere, a temporary area included, as the form reader of the framework would do.
/// </summary>
internal static class SubmissionForm
{
    internal const int FieldCharacters = 2048;

    private const string FileField = "file";
    private const string FormData = "multipart/form-data";
    private const int HeaderBytes = 16 * 1024;
    private const int HeaderCount = 16;
    private const int MostFields = 16;

    private static readonly Dictionary<string, MaterialForm> Forms = new(StringComparer.Ordinal)
    {
        ["conversation"] = MaterialForm.Conversation,
        ["comment"] = MaterialForm.Comment,
        ["document"] = MaterialForm.Document,
        ["labeled-sequence"] = MaterialForm.LabeledSequence,
    };

    /// <summary>Reads the fields up to the file; null when the body is not <c>multipart/form-data</c> with a boundary, and a body with too many parts before the file has none read.</summary>
    /// <param name="request">The request.</param>
    /// <param name="author">The person of the session.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    internal static async Task<SubmissionRead?> ReadAsync(HttpRequest request, long author, CancellationToken cancellationToken)
    {
        if (BoundaryOf(request) is not { } boundary)
        {
            return null;
        }

        var reader = new MultipartReader(boundary, request.Body) { HeadersCountLimit = HeaderCount, HeadersLengthLimit = HeaderBytes };
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        var seen = 0;
        while (await reader.ReadNextSectionAsync(cancellationToken).ConfigureAwait(false) is { } section)
        {
            if (++seen > MostFields)
            {
                break;
            }

            if (section.GetContentDispositionHeader() is not { } disposition || HeaderUtilities.RemoveQuotes(disposition.Name).Value is not { } name)
            {
                continue;
            }

            if (name == FileField)
            {
                return new SubmissionRead(Request(fields, author, section.Body, HeaderUtilities.RemoveQuotes(disposition.FileName).Value), true);
            }

            fields[name] = await ValueAsync(section, cancellationToken).ConfigureAwait(false);
        }

        return new SubmissionRead(Request(fields, author, Stream.Null, receivedName: null), false);
    }

    private static string? BoundaryOf(HttpRequest request) =>
        MediaTypeHeaderValue.TryParse(request.ContentType, out var type)
        && type.MediaType.Equals(FormData, StringComparison.OrdinalIgnoreCase)
        && HeaderUtilities.RemoveQuotes(type.Boundary).Value is { Length: > 0 } boundary
            ? boundary
            : null;

    private static async Task<string> ValueAsync(MultipartSection section, CancellationToken cancellationToken)
    {
        using var text = new StreamReader(section.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: FieldCharacters, leaveOpen: true);
        var buffer = new char[FieldCharacters + 1];
        var read = await text.ReadBlockAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
        return new string(buffer, 0, read);
    }

    private static SubmissionRequest Request(IReadOnlyDictionary<string, string> fields, long author, Stream content, string? receivedName) => new()
    {
        DomainId = int.TryParse(Field(fields, "domain"), NumberStyles.None, CultureInfo.InvariantCulture, out var domain) ? domain : 0,
        AuthorId = author,
        DeclaredForm = Forms.GetValueOrDefault(Field(fields, "declaredForm"), (MaterialForm)(-1)),
        SourceFormat = Field(fields, "sourceFormat"),
        Condition = Field(fields, "condition") is { Length: > 0 } condition ? condition : null,
        Origin = Field(fields, "origin") is { Length: > 0 } origin ? origin : null,
        OriginUnknown = Field(fields, "originUnknown") == "true",
        Content = content,
        ReceivedName = receivedName,
    };

    private static string Field(IReadOnlyDictionary<string, string> fields, string name) => fields.GetValueOrDefault(name, string.Empty);
}

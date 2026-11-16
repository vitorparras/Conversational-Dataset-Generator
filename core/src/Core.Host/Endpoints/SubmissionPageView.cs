using System.Globalization;
using Core.Host.Screens;
using Core.Host.Submissions;
using SubmissionFields = Core.Host.Screens.SubmissionFields;

namespace Core.Host.Endpoints;

/// <summary>The screen of the submission (#607) as a result: what the form offers, what was typed coming back after a sending, and the status of the answer. The file never comes back.</summary>
internal static class SubmissionPageView
{
    /// <summary>The page with the parameters given, its address of sending and its offer, answered with the status given.</summary>
    internal static IResult Page(HttpContext context, SubmissionFields.Choices offer, IDictionary<string, object?> fields, int status)
    {
        fields[nameof(SubmissionScreen.Action)] = ScreenPaths.BatchNew;
        fields[nameof(SubmissionScreen.Offer)] = offer;
        var result = ScreenResult.Of(context, typeof(SubmissionScreen), SubmissionScreen.Title, fields);
        result.StatusCode = status;
        return result;
    }

    /// <summary>The page after a sending: what was typed comes back in the form, the file never.</summary>
    internal static Dictionary<string, object?> Answered(SubmissionRead read, string key, object? value)
    {
        var request = read.Request;
        var domain = request.DomainId > 0 ? request.DomainId.ToString(CultureInfo.InvariantCulture) : null;
        var given = new SubmissionFields.Typed(domain, SubmissionForm.ValueOf(request.DeclaredForm), request.SourceFormat, request.Condition, request.Origin, request.OriginUnknown);
        return new Dictionary<string, object?>(StringComparer.Ordinal) { [nameof(SubmissionScreen.Given)] = given, [key] = value };
    }

    /// <summary>The domains granted by name, the formats the gate reads and the conditions the installation declared.</summary>
    internal static SubmissionFields.Choices OfferOf(Dictionary<int, string> granted, SubmissionService service, long maxBytes)
    {
        var domains = granted.OrderBy(pair => pair.Value, StringComparer.Ordinal)
            .Select(pair => new ChoiceGroup.Option(pair.Key.ToString(CultureInfo.InvariantCulture), pair.Value)).ToList();
        var (formats, conditions) = service.Offered;
        return new SubmissionFields.Choices(domains, formats, conditions, maxBytes);
    }
}

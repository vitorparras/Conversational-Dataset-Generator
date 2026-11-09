using Core.Boundary.Contracts;
using Core.Boundary.Text;

namespace Core.Ingestion.Tests.Deciding;

/// <summary>Occurrences and analyses built for the tests of the destinations, over texts that carry nothing but a marker.</summary>
internal static class Found
{
    public static Occurrence Occurrence(string kind, string partOfSpeech, string role, int start = 0, int end = 1) => new(start, end, kind, partOfSpeech, role);

    public static RecognitionOutcome Analysis(params Occurrence[] occurrences) => RecognitionOutcome.Analyzed(new AnalysisResponse(occurrences, null));

    public static Material Accepted(MaterialForm form, params ReadUnit[] units)
    {
        var conference = new FormConference(new TextBoundaries());
        return Assert.IsAssignableFrom<Material>(conference.Confer(form, Units.Read(units)).Accepted);
    }
}

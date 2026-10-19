using Core.Boundary.Contracts;

namespace Core.Boundary.Http;

/// <summary>Either the whole answer, every offset inside the text sent, or the one problem that refused it; never part of an answer.</summary>
public sealed record ResponseValidation
{
    private ResponseValidation(AnalysisResponse? response, ValidationProblem? problem)
    {
        Response = response;
        Problem = problem;
    }

    /// <summary>The answer, whole; null when it was refused.</summary>
    public AnalysisResponse? Response { get; }

    /// <summary>Why the answer was refused; null when it was accepted.</summary>
    public ValidationProblem? Problem { get; }

    internal static ResponseValidation Accepted(AnalysisResponse response) => new(response, null);

    internal static ResponseValidation Refused(ValidationProblem problem) => new(null, problem);
}

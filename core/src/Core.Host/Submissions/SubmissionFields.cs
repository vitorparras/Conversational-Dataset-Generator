namespace Core.Host.Submissions;

/// <summary>The fields of a request that are missing or invalid, by name and by code, before anything is read or recorded.</summary>
internal static class SubmissionFields
{
    /// <summary>The fields of the request that are missing or invalid; none when the request can be taken in.</summary>
    /// <param name="intake">What takes the file in, with the conditions the request may declare.</param>
    /// <param name="request">The request, whose file is not read.</param>
    internal static List<FieldProblem> Of(SubmissionIntake intake, SubmissionRequest request)
    {
        var problems = new[]
        {
            When(request.DomainId <= 0, "domain", "missing"),
            When(!Enum.IsDefined(request.DeclaredForm), "declared_form", "invalid"),
            When(string.IsNullOrWhiteSpace(request.SourceFormat), "source_format", "missing"),
            When(request.Condition is { } declared && intake.Conditions.RankOf(declared) < 0, "condition", "unknown"),
            SubmissionOrigin.Problem(request) is { } origin ? new FieldProblem("origin", origin) : null,
        }.OfType<FieldProblem>().ToList();
        return problems;
    }

    /// <summary>The fields at fault of a request whose condition must be chosen, as the form of the screen asks (#607 CA-1): those of <see cref="Of" /> and a condition left out, which the route of
    /// JSON takes as the first of the conditions declared instead.</summary>
    /// <param name="intake">What takes the file in, with the conditions the request may declare.</param>
    /// <param name="request">The request, whose file is not read.</param>
    internal static List<FieldProblem> Chosen(SubmissionIntake intake, SubmissionRequest request)
    {
        var problems = Of(intake, request);
        if (request.Condition is null)
        {
            problems.Add(new FieldProblem("condition", "missing"));
        }

        return problems;
    }

    private static FieldProblem? When(bool wrong, string field, string code) => wrong ? new FieldProblem(field, code) : null;
}

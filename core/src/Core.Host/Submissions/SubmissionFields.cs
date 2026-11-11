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

    private static FieldProblem? When(bool wrong, string field, string code) => wrong ? new FieldProblem(field, code) : null;
}

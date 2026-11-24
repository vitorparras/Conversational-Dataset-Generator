namespace Core.Boundary.Http;

/// <summary>Either the whole answer, every offset inside the text sent, or the one problem that refused it; never part of an answer.</summary>
/// <typeparam name="TResponse">The answer the contract declares for the request.</typeparam>
internal sealed record ResponseValidation<TResponse>
    where TResponse : class
{
    private ResponseValidation(TResponse? response, ValidationProblem? problem)
    {
        Response = response;
        Problem = problem;
    }

    /// <summary>The answer, whole; null when it was refused.</summary>
    internal TResponse? Response { get; }

    /// <summary>Why the answer was refused; null when it was accepted.</summary>
    internal ValidationProblem? Problem { get; }

    internal static ResponseValidation<TResponse> Accepted(TResponse response) => new(response, null);

    internal static ResponseValidation<TResponse> Refused(ValidationProblem problem) => new(null, problem);

    // The generated description lists public members only; these are internal now, so it is spelled out to keep the same line.
    public override string ToString() => $"ResponseValidation {{ Response = {Response}, Problem = {Problem} }}";
}

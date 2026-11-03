namespace Core.Host.Endpoints;

/// <summary>What the route that makes a person takes: the name of use, one of the functions of the policy and the first password, which is never answered back nor logged.</summary>
internal sealed record PersonAsked(string Name, string Function, string Password)
{
    /// <inheritdoc />
    public override string ToString() => nameof(PersonAsked);
}

namespace Core.Host.Endpoints;

/// <summary>What a person gives to change their own password: the one they have and the one they want; neither is answered back nor logged.</summary>
internal sealed record PasswordChangeAsked(string? Current, string? Replacement)
{
    /// <inheritdoc />
    public override string ToString() => nameof(PasswordChangeAsked);
}

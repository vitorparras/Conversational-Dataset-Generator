namespace Core.Host.Endpoints;

/// <summary>What the route that sets a password again takes; the password is never answered back nor logged.</summary>
internal sealed record PasswordAsked(string Password)
{
    /// <inheritdoc />
    public override string ToString() => nameof(PasswordAsked);
}

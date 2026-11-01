namespace Core.Host.Endpoints;

/// <summary>What a person gives to enter: the name of use and the password.</summary>
internal sealed record SignInAsked(string Name, string Password);

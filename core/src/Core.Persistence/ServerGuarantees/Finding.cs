using Core.Platform.Startup;

namespace Core.Persistence.ServerGuarantees;

/// <summary>What one guarantee concluded from the server's answer, before the probe names it and adds the server's version.</summary>
internal sealed record Finding(CheckStatus Status, string Message)
{
    internal static Finding Holds(string message) => new(CheckStatus.Passed, message);

    internal static Finding Missing(string whatToChange) => new(CheckStatus.Failed, whatToChange);
}

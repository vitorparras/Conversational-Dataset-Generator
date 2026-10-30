using System.Text;

namespace Core.Host.Supervision;

/// <summary>
/// Asks the launcher to take the standard error of the service, which the service otherwise shares with the core and so reaches the console of the operator.
/// Only tests that need to say why a service ended ask for it; the core never does.
/// </summary>
internal sealed class ErrorCollector
{
    /// <summary>How much of the end of the standard error is kept.</summary>
    internal int Limit { get; init; } = 4096;

    internal static Encoding Encoding { get; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
}

using System.Security.Cryptography;
using System.Text;

namespace Core.Ingestion.Sanitizing;

/// <summary>
/// The one declared list of steps every material goes through, in the order given. Its version is derived from the names and versions in that order and from nothing else, so that
/// the same list gives the same version in any process, and swapping two steps or changing one gives another (decision 0015).
/// </summary>
public sealed class SanitizationSteps
{
    /// <summary>The steps, in the order they run.</summary>
    /// <param name="steps">The declared steps; none may be missing.</param>
    public SanitizationSteps(IReadOnlyList<ISanitizationStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (steps.Any(step => step is null))
        {
            throw new ArgumentException("A declared step is missing.", nameof(steps));
        }

        Steps = [.. steps];
        Order = [.. Steps.Select(step => new StepIdentity(step.Name, step.Version))];
        var declared = string.Join("\n", Order.Select(step => $"{step.Name}@{step.Version}"));
        Version = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(declared)));
    }

    /// <summary>The steps, in the order they run.</summary>
    public IReadOnlyList<ISanitizationStep> Steps { get; }

    /// <summary>Who runs, in order.</summary>
    public IReadOnlyList<StepIdentity> Order { get; }

    /// <summary>The SHA-256, in lowercase hexadecimal, of each name@version on its own line, in order.</summary>
    public string Version { get; }
}

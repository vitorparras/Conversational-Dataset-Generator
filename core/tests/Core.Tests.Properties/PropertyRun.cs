using System.Diagnostics;
using System.Globalization;
using CsCheck;
using Xunit.Sdk;

namespace Core.Tests.Properties;

/// <summary>
/// The one way every property of this project is run, so the guarantees of decision 0007 hold for
/// all of them: a declared number of cases, a declared time budget that fails when exceeded, the
/// seed in every failure report, and a seed that can be given back to repeat the failure.
/// </summary>
internal static class PropertyRun
{
    /// <summary>Environment variable that repeats a run from the seed a failure reported.</summary>
    public const string SeedVariable = "CDG_PROPERTY_SEED";

    /// <summary>Cases generated per property. Lowering it to fit the time is a failure, not a setting.</summary>
    public const int Cases = 500;

    /// <summary>The time the declared number of cases may take before the property fails.</summary>
    public static readonly TimeSpan Budget = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Runs <paramref name="property"/> over <paramref name="cases"/> generated inputs. A failure
    /// carries the seed, the smallest counterexample found and the first input that failed; exceeding
    /// the time budget is a failure. The seed comes from <see cref="SeedVariable"/> when not given.
    /// </summary>
    public static void Check<T>(
        Gen<T> generator,
        Action<T> property,
        string? seed = null,
        int cases = Cases,
        TimeSpan? budget = null)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(property);

        var limit = budget ?? Budget;
        var firstFailing = string.Empty;
        var started = Stopwatch.StartNew();
        try
        {
            generator.Sample(
                value =>
                {
                    try
                    {
                        property(value);
                    }
                    catch (Exception) when (RememberFirst(ref firstFailing, value))
                    {
                        throw;
                    }
                },
                seed: seed ?? SeedFromEnvironment(),
                iter: cases,
                threads: 1,
                writeLine: _ => { });
        }
        catch (CsCheckException exception)
        {
            // The shrinking CsCheck does after the first failure is random, so its smallest case can
            // differ between two repeats of the same seed; the first failing input does not.
            throw new XunitException(
                $"property failed. {exception.Message}{Environment.NewLine}" +
                $"first failing input: {firstFailing}{Environment.NewLine}" +
                $"Repeat with {SeedVariable}=<the seed above>.",
                exception);
        }

        if (started.Elapsed > limit)
        {
            throw new XunitException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"time limit exceeded: {cases} cases took {started.Elapsed.TotalSeconds:F1} s, limit {limit.TotalSeconds:F1} s. The number of cases is declared and is not lowered to fit."));
        }
    }

    private static bool RememberFirst<T>(ref string firstFailing, T value)
    {
        if (firstFailing.Length == 0)
        {
            firstFailing = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "(null)";
        }

        return false;
    }

    private static string? SeedFromEnvironment()
    {
        var value = Environment.GetEnvironmentVariable(SeedVariable);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

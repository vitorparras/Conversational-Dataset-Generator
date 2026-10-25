using System.Diagnostics;
using Core.Persistence.Migrations;
using Core.Platform.Startup;

namespace Core.Host.Migration;

/// <summary>The mode the operator asks for with <c>migrate</c> as the first argument: the same executable, run once with the owner credential.</summary>
internal static class MigrationMode
{
    private const string Argument = "migrate";

    internal static bool Requested(string[] args) => args is [Argument, ..];

    /// <summary>The arguments the configuration reads: without the mode word when it was given.</summary>
    internal static string[] ConfigurationArguments(string[] args) => Requested(args) ? args[1..] : args;

    /// <summary>Applied ends as success; every other outcome has a code of its own, so a script tells them apart.</summary>
    internal static ExitCode ExitOf(MigrationOutcome outcome) => outcome switch
    {
        MigrationOutcome.Applied => ExitCode.Success,
        MigrationOutcome.NothingToDo => ExitCode.MigrationNothingToDo,
        MigrationOutcome.RefusedAhead => ExitCode.MigrationRefusedAhead,
        MigrationOutcome.RefusedDivergent => ExitCode.MigrationRefusedDivergent,
        MigrationOutcome.StepFailed => ExitCode.MigrationStepFailed,
        MigrationOutcome.NotConnected => ExitCode.MigrationNotConnected,
        _ => throw new UnreachableException(),
    };
}

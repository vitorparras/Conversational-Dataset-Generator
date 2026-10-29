using System.Diagnostics;
using Core.Persistence.Migrations;

namespace Core.Host.Migration;

/// <summary>Writes how a run of the migration ended: one line for each step applied, then one for the outcome, and one more when the failure of the step was written to the failure table.</summary>
internal static class MigrationReport
{
    internal static void Write(this ILogger logger, MigrationResult result)
    {
        foreach (var step in result.Applied)
        {
            logger.StepApplied(step);
        }

        switch (result.Outcome)
        {
            case MigrationOutcome.Applied:
                logger.SchemaMigrated(result.Found);
                break;
            case MigrationOutcome.NothingToDo:
                logger.NothingToMigrate(result.Found);
                break;
            case MigrationOutcome.RefusedAhead:
                logger.RefusedAhead(result.Found, result.Expected);
                break;
            case MigrationOutcome.RefusedDivergent:
                logger.RefusedDivergent(result.Step!, result.Registered!, result.Current!);
                break;
            case MigrationOutcome.StepFailed:
                logger.WriteStepFailed(result);
                break;
            case MigrationOutcome.NotConnected:
                logger.NotConnected(result.SqlState ?? "sem SQLSTATE");
                break;
            default:
                throw new UnreachableException();
        }
    }

    private static void WriteStepFailed(this ILogger logger, MigrationResult result)
    {
        logger.StepFailed(result.Step!, result.SqlState ?? "sem SQLSTATE", result.Found);
        if (result.FailureRecorded)
        {
            logger.FailureRecorded(result.Step!);
        }
    }
}

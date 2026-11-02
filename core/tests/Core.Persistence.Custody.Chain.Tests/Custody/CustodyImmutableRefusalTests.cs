using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Npgsql;

namespace Core.Persistence.Tests.Custody;

/// <summary>
/// The refusal of the triggers that keep an event unchanged is seen by the application as a named result,
/// with the words of whoever operates and nothing the server's message carried.
/// </summary>
public sealed class CustodyImmutableRefusalTests
{
    private const string Sequence = "7731";

    private static readonly CustodyEvent Pending = CustodyEvent.Of(
        CustodyAuthor.System, new CustodyItem(CustodyItemKind.Person, 1), CustodyStep.PersonCreated, CustodyReason.AdministrativeAct);

    [Theory]
    [InlineData("UPDATE")]
    [InlineData("DELETE")]
    [InlineData("TRUNCATE")]
    public void RefusalOfTheTriggersIsTheNamedResultAndNotTheServersText(string operation)
    {
        var failed = new PostgresException($"public.custody_event: {operation} refused for the event of sequence {Sequence}", "ERROR", "ERROR", "CE001");

        var result = CustodyTransaction.Refused(failed, Pending);

        Assert.Equal(CustodyOutcome.Immutable, result.Outcome);
        Assert.Null(result.Sequence);
        Assert.Equal(CustodyRefusals.Immutable().Refusal, result.Refusal);
        Assert.DoesNotContain(Sequence, result.Refusal, StringComparison.Ordinal);
        Assert.DoesNotContain(operation, result.Refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void SqlStateOfTheTriggersIsNotOneTheServerUsesForAnythingElse() =>
        Assert.Equal("CE001", CustodyStatements.ImmutableSqlState);

    [Fact]
    public void OtherRefusalOfTheServerIsStillADefectAndNotTheNamedResult()
    {
        var failed = new PostgresException("outra recusa", "ERROR", "ERROR", "42501");

        _ = Assert.Throws<InvalidOperationException>(() => CustodyTransaction.Refused(failed, Pending));
    }
}

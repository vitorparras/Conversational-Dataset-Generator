using Core.Persistence.Custody;
using Npgsql;

namespace Core.Persistence.Tests.Custody;

/// <summary>The refusal the chain gives while the last verification failed: it is a named result, written by the system and carrying no content.</summary>
public sealed class CustodyBrokenRefusalTests
{
    [Fact]
    public void RefusalWhileTheLastVerificationFailedIsTheNamedResultAndNotTheServersText()
    {
        var failed = new PostgresException("the custody chain failed its last verification: only a verification or a reading is added until the chain is restored", "ERROR", "ERROR", "CE002");
        var pending = CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.Person, 1), CustodyStep.PersonCreated, CustodyReason.AdministrativeAct);

        var result = CustodyTransaction.Refused(failed, pending);

        Assert.Equal(CustodyOutcome.ChainBroken, result.Outcome);
        Assert.Null(result.Sequence);
        Assert.Equal(CustodyRefusals.ChainBroken().Refusal, result.Refusal);
        Assert.EndsWith(".", result.Refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void SqlStateOfTheRefusalIsNotTheOneOfTheImmutability() =>
        Assert.NotEqual(CustodyStatements.ImmutableSqlState, CustodyStatements.ChainBrokenSqlState);

    [Fact]
    public void ContentRefusedWhileTheLastVerificationFailedSaysSoWithNothingOfTheContent()
    {
        var refusal = new CustodyChainBrokenException();

        Assert.Contains("nenhum conteúdo é gravado até a cadeia ser restaurada", refusal.Message, StringComparison.Ordinal);
    }
}

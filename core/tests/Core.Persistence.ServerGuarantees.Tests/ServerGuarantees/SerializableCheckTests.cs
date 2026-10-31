using Core.Persistence.ServerGuarantees;
using Core.Platform.Startup;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>The transaction asked for as serializable must run as serializable; any other level is named as found.</summary>
public sealed class SerializableCheckTests
{
    [Fact]
    public async Task UnreachableServerIsARefusalNamingTheCheck()
    {
        await using var server = new UnreachableServer();
        var check = new SerializableCheck(server.DataSource);

        var result = await check.RunAsync(TestContext.Current.CancellationToken);

        RefusalAssert.NamesTheCheckAndTheAddressWithoutTheSecret("Serializable", result, server);
    }

    [Fact]
    public void SerializableTransactionPasses()
    {
        var finding = SerializableCheck.Judge("serializable");

        Assert.Equal(CheckStatus.Passed, finding.Status);
        Assert.Equal("o servidor abre transação SERIALIZABLE para o papel da aplicação", finding.Message);
    }

    [Fact]
    public void CheckWithoutADataSourceIsADefect() =>
        _ = Assert.Throws<ArgumentNullException>(() => new SerializableCheck(null!));

    [Fact]
    public void OtherIsolationFailsNamingTheLevelFound()
    {
        var finding = SerializableCheck.Judge("read committed");

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains("'read committed'", finding.Message, StringComparison.Ordinal);
        Assert.EndsWith("conecte a aplicação direto ao servidor primário, sem intermediário que troque o isolamento", finding.Message, StringComparison.Ordinal);
    }
}

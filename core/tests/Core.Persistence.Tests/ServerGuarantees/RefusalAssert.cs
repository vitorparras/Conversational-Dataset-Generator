using Core.Platform.Startup;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>What every check answers when the server cannot be reached: a refusal under its own name, pointing at the configured address, without the secret.</summary>
internal static class RefusalAssert
{
    public static void NamesTheCheckAndTheAddressWithoutTheSecret(string checkName, CheckResult result, UnreachableServer server)
    {
        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Equal(checkName, result.Name);
        Assert.Equal("Database:Host", result.ExpectedSource);
        Assert.Equal("o servidor de banco não respondeu: confira o endereço configurado e se o servidor aceita conexões", result.Message);
        Assert.DoesNotContain(UnreachableServer.Password, result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(server.ConnectionString, result.Message, StringComparison.Ordinal);
    }
}

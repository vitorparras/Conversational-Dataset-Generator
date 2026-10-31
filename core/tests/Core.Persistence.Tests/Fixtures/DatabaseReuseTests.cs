namespace Core.Persistence.Tests.Fixtures;

/// <summary>What the mutation step and the test servers of a run agree on through the token: the names and the passwords.</summary>
public sealed class DatabaseReuseTests
{
    // nosemgrep: secret-literal-csharp is a synthetic run token of this test, not a credential.
    private const string Token = "zqx-token-0001";

    [Fact]
    public void ContainerAndImageAreNamedAfterTheToken()
    {
        Assert.Equal("cdg-test-database-zqx-token-0001", DatabaseReuse.ContainerName(Token));
        Assert.Equal("cdg-test-database:zqx-token-0001", DatabaseReuse.ImageName(Token));
    }

    [Fact]
    public void ARoleHasTheSamePasswordInEveryServerOfTheRun() =>
        Assert.Equal(DatabaseReuse.Password(Token, "owner"), DatabaseReuse.Password(Token, "owner"));

    [Fact]
    public void PasswordsDifferByRoleAndByToken()
    {
        var owner = DatabaseReuse.Password(Token, "owner");

        Assert.NotEqual(owner, DatabaseReuse.Password(Token, "app"));
        Assert.NotEqual(owner, DatabaseReuse.Password("zqx-token-0002", "owner"));
    }

    [Fact]
    public void APasswordIsLongHexadecimalAndNeverTheToken()
    {
        var password = DatabaseReuse.Password(Token, "app");

        Assert.Equal(64, password.Length);
        Assert.All(password, character => Assert.True(char.IsAsciiHexDigit(character)));
        Assert.DoesNotContain(Token, password, StringComparison.Ordinal);
    }
}

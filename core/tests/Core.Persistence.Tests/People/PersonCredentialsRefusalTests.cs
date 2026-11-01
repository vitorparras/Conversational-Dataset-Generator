using System.Text;
using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Persistence.Tests.ServerGuarantees;
using Npgsql;

namespace Core.Persistence.Tests.People;

/// <summary>
/// What the store of passwords refuses before it opens a connection, and what leaves when the connection fails (#69, CA-4): the data source here points at a port nobody listens on,
/// so a refusal that is no connection failure proves nothing was written, and the failure that comes out carries neither the password nor what the derivation left.
/// </summary>
public sealed class PersonCredentialsRefusalTests
{
    private const string Password = "ZQX-SENHA-9052";

    [Fact]
    public async Task EmptyPasswordIsRefusedAndWritesNothing()
    {
        await using var server = new UnreachableServer();

        var result = await Store(server).SetPasswordAsync(1, string.Empty, CustodyAuthor.System, TestContext.Current.CancellationToken);

        Assert.Equal(PersonOutcome.PasswordInvalid, result.Outcome);
        Assert.NotNull(result.Refusal);
        Assert.Null(result.PersonId);
    }

    [Fact]
    public async Task NoPasswordIsAnErrorWhenSetting()
    {
        await using var server = new UnreachableServer();

        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => Store(server).SetPasswordAsync(1, null!, CustodyAuthor.System, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task NoPasswordIsAnErrorWhenVerifying()
    {
        await using var server = new UnreachableServer();

        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => Store(server).VerifyAsync(1, null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void NoDataSourceIsAnError() =>
        Assert.Throws<ArgumentNullException>(() => new PersonCredentials(null!, new PasswordDerivation(1)));

    [Fact]
    public async Task NoDerivationIsAnError()
    {
        await using var server = new UnreachableServer();

        _ = Assert.Throws<ArgumentNullException>(() => new PersonCredentials(server.DataSource, null!));
    }

    [Fact]
    public async Task FailureOfTheConnectionWhileSettingCarriesNeitherThePasswordNorWhatItLeft()
    {
        await using var server = new UnreachableServer();

        var failure = await Assert.ThrowsAnyAsync<NpgsqlException>(() => Store(server).SetPasswordAsync(1, Password, CustodyAuthor.System, TestContext.Current.CancellationToken));

        AssertNothingOf(failure.ToString());
    }

    [Fact]
    public async Task FailureOfTheConnectionWhileVerifyingCarriesNeitherThePasswordNorWhatItLeft()
    {
        await using var server = new UnreachableServer();

        var failure = await Assert.ThrowsAnyAsync<NpgsqlException>(() => Store(server).VerifyAsync(1, Password, TestContext.Current.CancellationToken));

        AssertNothingOf(failure.ToString());
    }

    private static PersonCredentials Store(UnreachableServer server) => new(server.DataSource, new PasswordDerivation(1));

    private static void AssertNothingOf(string text)
    {
        var offered = new[] { Password, Convert.ToHexString(Encoding.UTF8.GetBytes(Password)), Convert.ToBase64String(Encoding.UTF8.GetBytes(Password)) };

        Assert.All(offered, secret => Assert.DoesNotContain(secret, text, StringComparison.OrdinalIgnoreCase));
    }
}

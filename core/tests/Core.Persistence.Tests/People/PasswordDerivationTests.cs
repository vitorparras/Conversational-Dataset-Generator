using System.Text;
using Core.Persistence.People;

namespace Core.Persistence.Tests.People;

/// <summary>The derivation of a password (#69, CA-1 and the factor without a value): what it derives, with which salt, and how it judges a password against what was stored.</summary>
public sealed class PasswordDerivationTests
{
    private const string Password = "ZQX-SENHA-4417";
    private const int Factor = 2;

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void WorkFactorThatIsNotPositiveIsRefusedAndNamed(int workFactor)
    {
        var refused = Assert.Throws<ArgumentOutOfRangeException>(() => new PasswordDerivation(workFactor));

        Assert.Equal("workFactor", refused.ParamName);
    }

    [Fact]
    public void WorkFactorGivenIsTheOneKept()
    {
        var derivation = new PasswordDerivation(Factor);

        Assert.Equal(Factor, derivation.WorkFactor);
    }

    [Fact]
    public void KnownVectorOfPbkdf2WithSha256IsReproduced()
    {
        var derivation = new PasswordDerivation(1);
        var stored = new Credential(
            PasswordDerivation.Algorithm,
            1,
            Encoding.ASCII.GetBytes("salt"),
            Convert.FromHexString("120fb6cffcf8b32c43e7225256c4f837a86548c92ccc35480805987cb70be17b"));

        Assert.True(derivation.Matches(stored, "password"));
    }

    [Fact]
    public void TheSamePasswordDerivedTwiceLeavesAnotherSaltAndAnotherResult()
    {
        var derivation = new PasswordDerivation(Factor);

        var first = derivation.Derive(Password);
        var second = derivation.Derive(Password);

        Assert.NotEqual(first.Salt, second.Salt);
        Assert.NotEqual(first.Derived, second.Derived);
    }

    [Fact]
    public void SaltHasAtLeastSixteenBytesAndTheResultThirtyTwo()
    {
        var credential = new PasswordDerivation(Factor).Derive(Password);

        Assert.True(credential.Salt.Length >= 16);
        Assert.Equal(PasswordDerivation.DerivedBytes, credential.Derived.Length);
    }

    [Fact]
    public void DerivationCarriesTheAlgorithmAndTheFactorItWasMadeWith()
    {
        var credential = new PasswordDerivation(Factor).Derive(Password);

        Assert.Equal("PBKDF2-HMAC-SHA256", credential.Algorithm);
        Assert.Equal(Factor, credential.WorkFactor);
    }

    [Fact]
    public void TheRightPasswordMatchesWhatItLeft()
    {
        var derivation = new PasswordDerivation(Factor);
        var stored = derivation.Derive(Password);

        Assert.True(derivation.Matches(stored, Password));
    }

    [Theory]
    [InlineData("ZQX-SENHA-4418")]
    [InlineData("ZQX-SENHA-4417 ")]
    [InlineData("")]
    public void AnotherPasswordDoesNotMatch(string offered)
    {
        var derivation = new PasswordDerivation(Factor);
        var stored = derivation.Derive(Password);

        Assert.False(derivation.Matches(stored, offered));
    }

    [Fact]
    public void ThePasswordInAnotherCaseDoesNotMatch()
    {
        var derivation = new PasswordDerivation(Factor);
        var stored = derivation.Derive(Password);

        Assert.False(derivation.Matches(stored, Password.Replace("ZQX", "Zqx", StringComparison.Ordinal)));
    }

    [Fact]
    public void PasswordIsCheckedWithTheFactorStoredAndNotTheCurrentOne()
    {
        var stored = new PasswordDerivation(Factor).Derive(Password);

        Assert.True(new PasswordDerivation(Factor + 5).Matches(stored, Password));
    }

    [Fact]
    public void PasswordIsCheckedWithTheSaltStored()
    {
        var derivation = new PasswordDerivation(Factor);
        var stored = derivation.Derive(Password);
        var otherSalt = new Credential(stored.Algorithm, stored.WorkFactor, derivation.Derive(Password).Salt, stored.Derived);

        Assert.False(derivation.Matches(otherSalt, Password));
    }

    [Fact]
    public void AnAlgorithmThisBuildDoesNotKnowNeverMatches()
    {
        var derivation = new PasswordDerivation(Factor);
        var stored = derivation.Derive(Password);
        var foreign = new Credential("zqx-algoritmo", stored.WorkFactor, stored.Salt, stored.Derived);

        Assert.False(derivation.Matches(foreign, Password));
    }

    [Fact]
    public void NothingStoredNeverMatches() =>
        Assert.False(new PasswordDerivation(Factor).Matches(null, Password));

    [Theory]
    [InlineData(1, 2, true)]
    [InlineData(2, 2, false)]
    [InlineData(3, 2, false)]
    public void ResultIsBehindOnlyWhenTheFactorStoredIsLowerThanTheCurrent(int stored, int current, bool behind)
    {
        var credential = new PasswordDerivation(stored).Derive(Password);

        Assert.Equal(behind, new PasswordDerivation(current).IsBehind(credential));
    }

    [Fact]
    public void CredentialPrintsNothingOfItself()
    {
        var credential = new PasswordDerivation(Factor).Derive(Password);

        Assert.Equal("Credential", credential.ToString());
    }
}

using System.Security.Cryptography;
using System.Text;

namespace Core.Persistence.People;

/// <summary>
/// Derives a password with PBKDF2 over HMAC-SHA256, with a salt of its own for each derivation. The work factor, the number of iterations, has no
/// value here: whoever builds the derivation gives it, so that raising it later is a configuration and not a change of code.
/// </summary>
public sealed class PasswordDerivation
{
    internal const string Algorithm = "PBKDF2-HMAC-SHA256";
    internal const int SaltBytes = 16;
    internal const int DerivedBytes = 32;

    private static readonly byte[] AbsentSalt = new byte[SaltBytes];

    /// <summary>The derivation runs with the work factor given.</summary>
    /// <param name="workFactor">The number of iterations; it must be positive.</param>
    public PasswordDerivation(int workFactor)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workFactor);
        WorkFactor = workFactor;
    }

    /// <summary>The work factor this derivation applies to every password it derives.</summary>
    public int WorkFactor { get; }

    internal Credential Derive(string password) => Derive(password, RandomNumberGenerator.GetBytes(SaltBytes), WorkFactor);

    /// <summary>
    /// Whether the password gives the result stored. With nothing stored it derives all the same and answers no,
    /// so that the time taken does not tell a person with a password from one without.
    /// </summary>
    internal bool Matches(Credential? stored, string password)
    {
        if (stored is null)
        {
            _ = Derive(password, AbsentSalt, WorkFactor);
            return false;
        }

        if (!string.Equals(stored.Algorithm, Algorithm, StringComparison.Ordinal))
        {
            return false;
        }

        var offered = Derive(password, stored.Salt, stored.WorkFactor);
        return CryptographicOperations.FixedTimeEquals(offered.Derived, stored.Derived);
    }

    internal bool IsBehind(Credential stored) => stored.WorkFactor < WorkFactor;

    private static Credential Derive(string password, byte[] salt, int workFactor)
    {
        var bytes = Encoding.UTF8.GetBytes(password);
        try
        {
            return new Credential(Algorithm, workFactor, salt, Rfc2898DeriveBytes.Pbkdf2(bytes, salt, workFactor, HashAlgorithmName.SHA256, DerivedBytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }
}

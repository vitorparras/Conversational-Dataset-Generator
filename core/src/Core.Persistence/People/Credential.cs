namespace Core.Persistence.People;

/// <summary>What the derivation of a password left: the algorithm and the work factor that made it, the salt of the person and the result. It prints nothing of itself.</summary>
internal sealed class Credential
{
    internal Credential(string algorithm, int workFactor, byte[] salt, byte[] derived)
    {
        Algorithm = algorithm;
        WorkFactor = workFactor;
        Salt = salt;
        Derived = derived;
    }

    internal string Algorithm { get; }

    internal int WorkFactor { get; }

    internal byte[] Salt { get; }

    internal byte[] Derived { get; }

    public override string ToString() => nameof(Credential);
}

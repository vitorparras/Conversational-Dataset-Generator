internal static class Sample
{
    // ruleid: secret-literal-csharp
    private const string Password = "planted-value-pw";
    // ok: secret-literal-csharp
    public const string DefaultToken = "none";
    // ruleid: secret-literal-csharp
    private const string ApiKey = "planted-value-key";
    internal static void M()
    {
        // ruleid: secret-literal-csharp
        var secret = "planted-value-local";
        // ruleid: secret-literal-csharp
        var cs = "Host=h;Username=u;Password=planted";
    }
}

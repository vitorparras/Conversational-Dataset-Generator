internal static class Sample
{
    // ruleid: secret-literal-csharp
    private const string Password = "planted-value-pw";
    // ok: secret-literal-csharp
    public const string DefaultToken = "none";
    // ruleid: secret-literal-csharp
    private const string ApiKey = "planted-value-key";
    // ruleid: secret-literal-csharp
    private const string EncryptionKey = "planted-value-encryption";
    // ok: secret-literal-csharp
    private const string UrlsKey = "Host:Urls";

    // ruleid: secret-literal-csharp
    internal static string Sign(string signingKey = "planted-value-signing") => signingKey + UrlsKey;

    internal static void M()
    {
        // ruleid: secret-literal-csharp
        var secret = "planted-value-local";
        // ruleid: secret-literal-csharp
        var cs = "Host=h;Username=u;Password=planted";
    }
}

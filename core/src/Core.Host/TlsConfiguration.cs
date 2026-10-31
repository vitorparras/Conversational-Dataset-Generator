using System.Security.Cryptography.X509Certificates;
using Core.Platform.Configuration;
using Core.Platform.Tls;
using ConfigurationSection = Core.Platform.Configuration.ConfigurationSection;

namespace Core.Host;

/// <summary>
/// The channel's material and the addresses it serves: the paths of the certificate and of the private key, never the key, with no
/// default because a default would serve with a credential nobody chose, and addresses that are all encrypted, because there is no
/// plain port to redirect from.
/// </summary>
internal static class TlsConfiguration
{
    internal const string CertificatePath = SectionName + ":" + CertificateKey;

    internal const string PrivateKeyPath = SectionName + ":" + PrivateKeyKey;

    private const string SectionName = "Tls";
    private const string CertificateKey = "CertificateFile";
    private const string PrivateKeyKey = "KeyFile";
    private const string Scheme = "https://";
    private const string Accepted = "endereços https, separados por ponto e vírgula; o sistema não serve em claro";

    internal static ConfigurationSection Section { get; } =
        new(SectionName, [ConfigurationKey.Protection(CertificateKey).AsSensitive(), ConfigurationKey.Protection(PrivateKeyKey).AsSensitive()]);

    /// <summary>The same section for the migration mode, which serves nothing: the paths may be given, and are then checked as text, but are not required.</summary>
    internal static ConfigurationSection SectionWhenMigrating { get; } =
        new(SectionName, [ConfigurationKey.Optional(CertificateKey).AsSensitive(), ConfigurationKey.Optional(PrivateKeyKey).AsSensitive()]);

    /// <summary>The source of the files the approved values name, which refuses a private key that lies in the folder the host runs from.</summary>
    internal static TlsMaterialSource SourceOf(IReadOnlyDictionary<string, string> values) =>
        new(values[CertificatePath], values[PrivateKeyPath], AppContext.BaseDirectory, TimeProvider.System);

    /// <summary>The refusal of an address that is not encrypted, which the host would otherwise serve in clear next to the encrypted ones.</summary>
    internal static IReadOnlyList<ConfigurationProblem> Refusals(IReadOnlyDictionary<string, string> values) =>
        values.TryGetValue(HostConfiguration.UrlsPath, out var urls) && Array.Exists(Addresses(urls), url => !url.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase))
            ? [new ConfigurationProblem(ConfigurationProblemKind.ValueNotAccepted, HostConfiguration.UrlsPath, urls, [Accepted])]
            : [];

    /// <summary>The builder, whose server presents the certificate the check in the startup services read.</summary>
    internal static WebApplicationBuilder Serve(WebApplicationBuilder builder, IServiceProvider startup)
    {
        var certificate = Servable(startup.GetRequiredService<TlsMaterialCheck>());
        _ = builder.WebHost.ConfigureKestrel(options => options.ConfigureHttpsDefaults(https => https.ServerCertificate = certificate));
        return builder;
    }

    /// <summary>On Windows a key made in memory is not one the system's channel can use, so the certificate goes through a file image first.</summary>
    private static X509Certificate2 Servable(TlsMaterialCheck check) =>
        OperatingSystem.IsWindows() ? X509CertificateLoader.LoadPkcs12(check.Certificate!.Export(X509ContentType.Pfx), password: null) : check.Certificate!;

    private static string[] Addresses(string urls) => urls.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}

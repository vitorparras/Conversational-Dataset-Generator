using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using Core.Platform.Tls;
using Core.Tests.Support.Custody;

namespace Core.Platform.Tests.Tls;

/// <summary>The channel's material as the means of delivery: what the files give when they are right, and what each refusal says, which is never the key.</summary>
public sealed class TlsMaterialSourceTests : IDisposable
{
    private const string NotPrivate =
        "o PEM não traz uma chave privada (PRIVATE KEY, RSA PRIVATE KEY ou EC PRIVATE KEY), e uma chave pública ou um certificado não serve";

    private readonly CustodyFiles files = new();

    public void Dispose() => files.Dispose();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FilesOfTheRightFormGiveTheCertificateWithItsPrivateKey(bool rsa)
    {
        var source = Source(SyntheticCertificate.Valid(rsa));

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Empty(reading.Problems);
        Assert.True(reading.Certificate!.HasPrivateKey);
        Assert.Equal("CN=zqx-teste", reading.Certificate.Subject);
    }

    [Fact]
    public void ConfigurationKeysAreTheOnesTheDecisionNames()
    {
        Assert.Equal("Tls:CertificateFile", TlsMaterialSource.CertificateKey);
        Assert.Equal("Tls:KeyFile", TlsMaterialSource.PrivateKeyKey);
    }

    [Fact]
    public async Task CertificateAbsentRefusesNamingTheFileItsKeyAndItsPath()
    {
        var absent = Path.Combine(files.Directory, "sem-certificado");
        var source = new TlsMaterialSource(absent, files.WriteText("key", SyntheticCertificate.Valid().KeyPem), AppContext.BaseDirectory, Clock);

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Null(reading.Certificate);
        Assert.Equal([$"arquivo do certificado (Tls:CertificateFile) não encontrado em '{absent}'"], reading.Problems);
    }

    [Fact]
    public async Task KeyAbsentRefusesNamingTheFileItsKeyAndItsPath()
    {
        var absent = Path.Combine(files.Directory, "sem-chave");
        var source = new TlsMaterialSource(files.WriteText("certificate", SyntheticCertificate.Valid().CertificatePem), absent, AppContext.BaseDirectory, Clock);

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal([$"arquivo da chave privada (Tls:KeyFile) não encontrado em '{absent}'"], reading.Problems);
    }

    [Fact]
    public async Task BothFilesAbsentRefuseNamingBoth()
    {
        var source = new TlsMaterialSource(Path.Combine(files.Directory, "a"), Path.Combine(files.Directory, "b"), AppContext.BaseDirectory, Clock);

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, reading.Problems.Count);
        Assert.Contains("arquivo do certificado (Tls:CertificateFile) não encontrado", reading.Problems[0], StringComparison.Ordinal);
        Assert.Contains("arquivo da chave privada (Tls:KeyFile) não encontrado", reading.Problems[1], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("isto não é PEM")]
    [InlineData("")]
    public async Task CertificateThatIsNotPemRefusesNamingTheFile(string content)
    {
        var source = Source(new SyntheticCertificate(content, SyntheticCertificate.Valid().KeyPem));

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal([$"arquivo do certificado (Tls:CertificateFile) em '{CertificatePath}': não é um certificado em PEM"], reading.Problems);
    }

    [Fact]
    public async Task PemOfAnotherKindInTheCertificateFileIsNotACertificate()
    {
        var material = SyntheticCertificate.Valid();
        var source = Source(new SyntheticCertificate(material.KeyPem, material.KeyPem));

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Contains("não é um certificado em PEM", Assert.Single(reading.Problems), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CertificateWithPemFramingAndGarbageInsideIsUnreadable()
    {
        var garbage = "-----BEGIN CERTIFICATE-----\n" + Convert.ToBase64String(Encoding.ASCII.GetBytes("zqx não é um certificado")) + "\n-----END CERTIFICATE-----\n";
        var source = Source(new SyntheticCertificate(garbage, SyntheticCertificate.Valid().KeyPem));

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal([$"arquivo do certificado (Tls:CertificateFile) em '{CertificatePath}': PEM de certificado ilegível"], reading.Problems);
    }

    [Fact]
    public async Task CertificateFileThatIsNotTextRefusesAsNotPem()
    {
        var material = SyntheticCertificate.Valid();
        var source = new TlsMaterialSource(files.Write("certificate", [0xFF, 0xFE, 0xFD, 0x80]), files.WriteText("key", material.KeyPem), AppContext.BaseDirectory, Clock);

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Contains("não é um certificado em PEM", Assert.Single(reading.Problems), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("isto não é PEM")]
    [InlineData("")]
    public async Task KeyThatIsNotPemRefusesNamingTheFile(string content)
    {
        var source = Source(new SyntheticCertificate(SyntheticCertificate.Valid().CertificatePem, content));

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal([$"arquivo da chave privada (Tls:KeyFile) em '{KeyPath}': não é uma chave privada em PEM"], reading.Problems);
    }

    [Fact]
    public async Task KeyProtectedByAPasswordIsRefusedSayingSo()
    {
        var encrypted = Framed("ENCRYPTED PRIVATE KEY", Convert.ToBase64String(new byte[48]));
        var source = Source(new SyntheticCertificate(SyntheticCertificate.Valid().CertificatePem, encrypted));

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal([$"arquivo da chave privada (Tls:KeyFile) em '{KeyPath}': chave protegida por senha; a chave fica sem senha, só com permissão do dono"], reading.Problems);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublicKeyOfTheCertificateInTheKeyFileIsRefusedNamingTheFile(bool rsa)
    {
        var made = SyntheticCertificate.Valid(rsa);
        using var parsed = X509Certificate2.CreateFromPem(made.CertificatePem);
        var publicKey = new string(PemEncoding.Write("PUBLIC KEY", parsed.PublicKey.ExportSubjectPublicKeyInfo()));
        var source = Source(new SyntheticCertificate(made.CertificatePem, publicKey));

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [$"arquivo da chave privada (Tls:KeyFile) em '{KeyPath}': {NotPrivate}"],
            reading.Problems);
    }

    [Fact]
    public async Task CertificateInTheKeyFileIsRefusedNamingTheFile()
    {
        var made = SyntheticCertificate.Valid();
        var source = Source(new SyntheticCertificate(made.CertificatePem, made.CertificatePem));

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [$"arquivo da chave privada (Tls:KeyFile) em '{KeyPath}': {NotPrivate}"],
            reading.Problems);
    }

    [Theory]
    [InlineData("PRIVATE KEY")]
    [InlineData("RSA PRIVATE KEY")]
    [InlineData("EC PRIVATE KEY")]
    public async Task KeyWithPemFramingAndGarbageInsideIsUnreadable(string label)
    {
        var garbage = $"-----BEGIN {label}-----\n{Convert.ToBase64String(Encoding.ASCII.GetBytes("zqx não é uma chave"))}\n-----END {label}-----\n";
        var source = Source(new SyntheticCertificate(SyntheticCertificate.Valid().CertificatePem, garbage));

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [$"arquivo da chave privada (Tls:KeyFile) em '{KeyPath}': PEM de chave privada ilegível ou de um tipo que o canal não usa; aceitas RSA e curva elíptica"],
            reading.Problems);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task KeyThatDoesNotFormAPairWithTheCertificateRefusesNamingBothFiles(bool certificateIsRsa, bool keyIsRsa)
    {
        var certificate = SyntheticCertificate.Valid(certificateIsRsa);
        var source = Source(new SyntheticCertificate(certificate.CertificatePem, SyntheticCertificate.Valid(keyIsRsa).KeyPem));

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Null(reading.Certificate);
        Assert.Equal(
            [$"arquivo do certificado (Tls:CertificateFile) em '{CertificatePath}' e arquivo da chave privada (Tls:KeyFile) em '{KeyPath}': a chave privada não é a do certificado"],
            reading.Problems);
    }

    [Fact]
    public async Task KeyOfTheSameKindAsTheCertificateButAnotherPairRefuses()
    {
        var source = Source(new SyntheticCertificate(SyntheticCertificate.Valid().CertificatePem, SyntheticCertificate.Valid().KeyPem));

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Contains("a chave privada não é a do certificado", Assert.Single(reading.Problems), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExpiredCertificateRefusesTellingWhenItExpired()
    {
        var source = Source(SyntheticCertificate.Create(SyntheticCertificate.Now.AddDays(-30), SyntheticCertificate.Now.AddSeconds(-1)));

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal([$"arquivo do certificado (Tls:CertificateFile) em '{CertificatePath}': certificado vencido em 2026-10-31T11:59:59Z"], reading.Problems);
    }

    [Fact]
    public async Task CertificateNotYetValidRefusesTellingFromWhenItIs()
    {
        var source = Source(SyntheticCertificate.Create(SyntheticCertificate.Now.AddSeconds(1), SyntheticCertificate.Now.AddDays(30)));

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal([$"arquivo do certificado (Tls:CertificateFile) em '{CertificatePath}': certificado ainda não válido, vale a partir de 2026-10-31T12:00:01Z"], reading.Problems);
    }

    [Fact]
    public async Task CertificateIsJudgedAtTheMomentOfTheClockOfTheSource()
    {
        var material = SyntheticCertificate.Valid();
        var afterwards = new FixedClock(SyntheticCertificate.Now.AddDays(2));
        var late = new TlsMaterialSource(files.WriteText("certificate", material.CertificatePem), files.WriteText("key", material.KeyPem), AppContext.BaseDirectory, afterwards);

        using var reading = await late.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Contains("certificado vencido", Assert.Single(reading.Problems), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CertificateOnTheLastSecondOfItsValidityIsStillValid()
    {
        var source = Source(SyntheticCertificate.Create(SyntheticCertificate.Now.AddDays(-1), SyntheticCertificate.Now));

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Empty(reading.Problems);
    }

    [Fact]
    public async Task CertificateOnTheFirstSecondOfItsValidityIsAlreadyValid()
    {
        var source = Source(SyntheticCertificate.Create(SyntheticCertificate.Now, SyntheticCertificate.Now.AddDays(1)));

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Empty(reading.Problems);
    }

    [Fact]
    public async Task EveryProblemComesInOneReading()
    {
        var expired = SyntheticCertificate.Create(SyntheticCertificate.Now.AddDays(-30), SyntheticCertificate.Now.AddDays(-1));
        var source = Source(new SyntheticCertificate(expired.CertificatePem, "isto não é PEM"));

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, reading.Problems.Count);
        Assert.Contains("certificado vencido", reading.Problems[0], StringComparison.Ordinal);
        Assert.Contains("não é uma chave privada em PEM", reading.Problems[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task KeyInsideTheArtifactFolderRefusesNamingTheFileAndNotReadingIt()
    {
        var material = SyntheticCertificate.Valid();
        var source = new TlsMaterialSource(files.WriteText("certificate", material.CertificatePem), files.WriteText("key", "isto não é PEM"), files.Directory, Clock);

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal([$"arquivo da chave privada (Tls:KeyFile) em '{KeyPath}', dentro da pasta do artefato; ele fica fora dela"], reading.Problems);
    }

    [Fact]
    public async Task CertificateInsideTheArtifactFolderIsNotRefusedBecauseItIsNotASecret()
    {
        using var elsewhere = new CustodyFiles();
        var material = SyntheticCertificate.Valid();
        var source = new TlsMaterialSource(files.WriteText("certificate", material.CertificatePem), elsewhere.WriteText("key", material.KeyPem), files.Directory, Clock);

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Empty(reading.Problems);
    }

    [Fact]
    public async Task KeyInASiblingFolderWithTheSamePrefixIsNotInsideTheArtifactFolder()
    {
        var material = SyntheticCertificate.Valid();
        var source = new TlsMaterialSource(files.WriteText("certificate", material.CertificatePem), files.WriteText("key", material.KeyPem), files.Directory + "-irmao", Clock);

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Empty(reading.Problems);
    }

    [Fact]
    public async Task KeyReachedThroughALinkedFolderThatPointsInsideTheArtifactFolderIsRefused()
    {
        using var artifact = new CustodyFiles();
        var material = SyntheticCertificate.Valid();
        _ = artifact.WriteText("key", material.KeyPem);
        var folder = Path.Combine(files.Directory, "current");
        Assert.SkipUnless(TryFolderLink(folder, artifact.Directory), "a plataforma não deixou criar o elo de pasta");
        var source = new TlsMaterialSource(files.WriteText("certificate", material.CertificatePem), Path.Combine(folder, "key"), artifact.Directory, Clock);

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Contains("dentro da pasta do artefato", Assert.Single(reading.Problems), StringComparison.Ordinal);
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public async Task KeyOnLinuxOpenToTheGroupIsRefusedNamingTheModeAndNotReadingIt()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "o modo de arquivo é do Linux");
        var material = SyntheticCertificate.Valid();
        var key = files.WriteText("key", material.KeyPem);
        File.SetUnixFileMode(key, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
        var source = new TlsMaterialSource(files.WriteText("certificate", material.CertificatePem), key, AppContext.BaseDirectory, Clock);

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [$"arquivo da chave privada (Tls:KeyFile) em '{key}': permissão aberta a grupo ou outros (modo 640); só o dono do processo pode ter acesso, como no modo 600"],
            reading.Problems);
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public async Task CertificateOnLinuxOpenToOthersIsNotRefusedBecauseItIsNotASecret()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "o modo de arquivo é do Linux");
        var material = SyntheticCertificate.Valid();
        var certificate = files.WriteText("certificate", material.CertificatePem);
        File.SetUnixFileMode(certificate, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        var source = new TlsMaterialSource(certificate, files.WriteText("key", material.KeyPem), AppContext.BaseDirectory, Clock);

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Empty(reading.Problems);
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task KeyOnWindowsOpenToUsersIsRefusedNamingTheFile()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "a lista de acesso é do Windows");
        var material = SyntheticCertificate.Valid();
        var key = files.WriteText("key", material.KeyPem);
        var file = new FileInfo(key);
        var security = file.GetAccessControl();
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null), FileSystemRights.Read, AccessControlType.Allow));
        file.SetAccessControl(security);
        var source = new TlsMaterialSource(files.WriteText("certificate", material.CertificatePem), key, AppContext.BaseDirectory, Clock);

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Contains($"arquivo da chave privada (Tls:KeyFile) em '{key}': permissão concedida a quem não é o usuário do processo", Assert.Single(reading.Problems), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FileLargerThanItsKindCanBeRefusesNamingTheLimit()
    {
        var material = SyntheticCertificate.Valid();
        var source = new TlsMaterialSource(
            files.WriteText("certificate", material.CertificatePem + new string('#', 65537)),
            files.WriteText("key", material.KeyPem + new string('#', 16385)),
            AppContext.BaseDirectory,
            Clock);

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, reading.Problems.Count);
        Assert.EndsWith("maior que 65536 bytes, o que um arquivo desse tipo não é", reading.Problems[0], StringComparison.Ordinal);
        Assert.EndsWith("maior que 16384 bytes, o que um arquivo desse tipo não é", reading.Problems[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task FileThatFillsTheLimitExactlyIsRead()
    {
        var material = SyntheticCertificate.Valid();
        var source = new TlsMaterialSource(
            files.WriteText("certificate", material.CertificatePem + "\n" + new string('#', 65535 - material.CertificatePem.Length)),
            files.WriteText("key", material.KeyPem + "\n" + new string('#', 16383 - material.KeyPem.Length)),
            AppContext.BaseDirectory,
            Clock);

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Empty(reading.Problems);
    }

    [Fact]
    public async Task FileThatCannotBeOpenedRefusesNamingTheKindOfFailureAndNoContent()
    {
        var material = SyntheticCertificate.Valid();
        var source = Source(material);
        await using var held = new FileStream(CertificatePath, FileMode.Open, FileAccess.Read, FileShare.None);

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal([$"arquivo do certificado (Tls:CertificateFile) em '{CertificatePath}': não pôde ser aberto (IOException)"], reading.Problems);
    }

    [Fact]
    public async Task NothingTheReadingTellsCarriesTheKey()
    {
        var material = SyntheticCertificate.Valid();
        var body = material.KeyPem.Split('\n')[1];
        var other = SyntheticCertificate.Valid();
        var source = Source(new SyntheticCertificate(other.CertificatePem, material.KeyPem));

        using var refused = await source.ReadAsync(TestContext.Current.CancellationToken);
        using var ready = await Source(material).ReadAsync(TestContext.Current.CancellationToken);

        Assert.NotEmpty(refused.Problems);
        Assert.DoesNotContain(body, string.Join('\n', refused.Problems), StringComparison.Ordinal);
        Assert.DoesNotContain(body, refused.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(body, ready.ToString(), StringComparison.Ordinal);
        Assert.Equal("TLS material ready", ready.ToString());
        Assert.Equal("TLS material refused (1 problems)", refused.ToString());
    }

    [Fact]
    public void ReadingOfNoProblemIsADefect() =>
        _ = Assert.Throws<ArgumentException>(() => TlsMaterialReading.Refused([]));

    [Fact]
    public void SourceWithoutAPathOrAClockIsADefect()
    {
        var clock = Clock;
        _ = Assert.Throws<ArgumentException>(() => new TlsMaterialSource(" ", "b", "c", clock));
        _ = Assert.Throws<ArgumentException>(() => new TlsMaterialSource("a", " ", "c", clock));
        _ = Assert.Throws<ArgumentException>(() => new TlsMaterialSource("a", "b", " ", clock));
        _ = Assert.Throws<ArgumentNullException>(() => new TlsMaterialSource("a", "b", "c", null!));
    }

    private static bool TryFolderLink(string link, string target)
    {
        try
        {
            _ = Directory.CreateSymbolicLink(link, target);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static FixedClock Clock { get; } = new(SyntheticCertificate.Now);

    /// <summary>The PEM framing built from the label, so that no line of the file reads as a key block to a secret scanner.</summary>
    private static string Framed(string label, string body) => $"-----BEGIN {label}-----\n{body}\n-----END {label}-----\n";

    private string CertificatePath => Path.Combine(files.Directory, "certificate");

    private string KeyPath => Path.Combine(files.Directory, "key");

    private TlsMaterialSource Source(SyntheticCertificate material) =>
        new(files.WriteText("certificate", material.CertificatePem), files.WriteText("key", material.KeyPem), AppContext.BaseDirectory, Clock);
}

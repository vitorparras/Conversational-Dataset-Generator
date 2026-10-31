using System.Text;
using Core.Platform.Startup;
using Core.Tests.Support.Custody;

namespace Core.Host.Tests;

/// <summary>
/// The built host starts only with the custody files in place: what the operator reads when it does not,
/// what the real installation also declares, and that no spelling of the key ever reaches a stream.
/// </summary>
public sealed class MasterKeyStartTests : IDisposable
{
    private static readonly byte[] Key = CustodyFiles.SyntheticKey();

    private readonly CustodyFiles files = new();

    public void Dispose() => files.Dispose();

    [Fact]
    public async Task ProcessWithoutTheCustodyFilesEndsWithTheRefusalCodeAndNamesBothKeys()
    {
        var installation = HostInstallation.Built(HostProcess.FreeAddress()) with { ProvidesMasterKey = false };

        var exit = await HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, TestContext.Current.CancellationToken);

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("'MasterKey:File'", exit.Error, StringComparison.Ordinal);
        Assert.Contains("'MasterKey:LabelsFile'", exit.Error, StringComparison.Ordinal);
        Assert.Contains("não tem valor padrão", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessWhoseKeyFileIsNotThereEndsWithTheCheckCodeAndNamesTheFileAndItsPath()
    {
        var missing = Path.Combine(files.Directory, "nao-existe");

        var exit = await RunAsync(missing, files.Write("labels", []));

        Assert.Equal((int)ExitCode.StartupCheckFailed, exit.Code);
        Assert.Contains($"chave-mestra ausente: arquivo da chave não encontrado em '{missing}'", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessWhoseKeyIsOfTheWrongSizeNamesTheSizeAndShowsNoSpellingOfTheValue()
    {
        var short16 = Key[..16];
        var keyPath = files.Write("key", Base64Line(short16));
        var exit = await RunAsync(keyPath, files.Write("labels", []));

        Assert.Equal((int)ExitCode.StartupCheckFailed, exit.Code);
        Assert.Contains($"chave-mestra de tamanho inválido: encontrado arquivo da chave em '{keyPath}': 16 bytes, esperados 32", exit.Error, StringComparison.Ordinal);
        AssertNoSpelling(short16, exit.Error + exit.Output);
    }

    [Fact]
    public async Task ProcessWhoseKeyFileIsNotBase64NamesWhatItFoundAndNeverTheText()
    {
        const string Text = "zqx-isto-nao-e-uma-chave";
        var exit = await RunAsync(files.Write("key", Encoding.ASCII.GetBytes(Text + "\n")), files.Write("labels", []));

        Assert.Equal((int)ExitCode.StartupCheckFailed, exit.Code);
        Assert.Contains("chave-mestra ilegível", exit.Error, StringComparison.Ordinal);
        Assert.Contains($"não é base64, {Text.Length} bytes", exit.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(Text, exit.Error + exit.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessWhoseKeyFileIsInsideTheFolderOfTheHostIsRefusedNamingTheFile()
    {
        var inside = Path.Combine(AppContext.BaseDirectory, $"zqx-chave-{Guid.NewGuid():N}");
        try
        {
            await File.WriteAllBytesAsync(inside, Base64Line(Key), TestContext.Current.CancellationToken);
            CustodyFiles.Restrict(inside);

            var exit = await RunAsync(inside, files.Write("labels", []));

            Assert.Equal((int)ExitCode.StartupCheckFailed, exit.Code);
            Assert.Contains($"arquivo da chave em '{inside}', dentro da pasta do artefato", exit.Error, StringComparison.Ordinal);
            AssertNoSpelling(Key, exit.Error + exit.Output);
        }
        finally
        {
            File.Delete(inside);
        }
    }

    [Fact]
    public async Task ProcessThatStartsWithTheKeyWritesNoSpellingOfItToAnyStream()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var baseAddress = HostProcess.FreeAddress();
        var installation = Installation(baseAddress, files.Write("key", Base64Line(Key)), files.Write("labels", []));
        using var host = await HostProcess.StartAsync(installation, baseAddress, HostProcess.StartupTimeout, cancellationToken);
        using var status = await host.GetAsync("/status", cancellationToken);
        var body = await status.Content.ReadAsStringAsync(cancellationToken);

        var streams = await host.StopWithErrorAsync();

        Assert.Contains("\"MasterKey\"", body, StringComparison.Ordinal);
        AssertNoSpelling(Key, body + streams.Output + streams.Error);
        Assert.DoesNotContain(files.Directory, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RealInstallationWithoutTheCustodyDeclarationEndsWithTheRefusalCodeAndNamesBothKeys()
    {
        var installation = Installation(HostProcess.FreeAddress(), CustodyFiles.Shared.KeyPath, CustodyFiles.Shared.LabelsPath, "real");

        var exit = await HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, TestContext.Current.CancellationToken);

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("'Custody:LastRecoveryTest'", exit.Error, StringComparison.Ordinal);
        Assert.Contains("'Custody:Holder'", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RealInstallationWithTheDeclarationStartsAndSaysItInTheStartupLineWithoutTheHolder()
    {
        var baseAddress = HostProcess.FreeAddress();
        var cancellationToken = TestContext.Current.CancellationToken;
        var real = Installation(baseAddress, CustodyFiles.Shared.KeyPath, CustodyFiles.Shared.LabelsPath, "real");
        var installation = real with { Arguments = [.. real.Arguments, "--Custody:LastRecoveryTest=2026-10-01", "--Custody:Holder=PESSOA_1"] };
        using var host = await HostProcess.StartAsync(installation, baseAddress, HostProcess.StartupTimeout, cancellationToken);

        var output = await host.StopAsync();

        Assert.Contains("Instalação real", output, StringComparison.Ordinal);
        Assert.Contains("LastRecoveryTest=2026-10-01", output, StringComparison.Ordinal);
        Assert.DoesNotContain("PESSOA_1", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DemonstrationStartsWithoutTheCustodyDeclaration()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await HostProcess.StartAsync(cancellationToken);

        using var response = await host.GetAsync("/status", cancellationToken);

        Assert.True(response.IsSuccessStatusCode);
    }

    private static byte[] Base64Line(byte[] key) => Encoding.ASCII.GetBytes(Convert.ToBase64String(key) + "\n");

    private static void AssertNoSpelling(byte[] key, string text) =>
        Assert.All(
            [Convert.ToHexString(key), Convert.ToHexStringLower(key), Convert.ToBase64String(key)],
            spelling => Assert.DoesNotContain(spelling, text, StringComparison.Ordinal));

    private static HostInstallation Installation(Uri baseAddress, string keyPath, string labelsPath, string kind = "demonstracao")
    {
        var built = HostInstallation.Built(baseAddress);
        return built with
        {
            DeclaresInstallation = false,
            Arguments = [.. built.Arguments, $"--Installation:Kind={kind}", $"--MasterKey:File={keyPath}", $"--MasterKey:LabelsFile={labelsPath}"],
        };
    }

    private static async Task<HostExit> RunAsync(string keyPath, string labelsPath)
    {
        var installation = Installation(HostProcess.FreeAddress(), keyPath, labelsPath);
        return await HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, TestContext.Current.CancellationToken);
    }
}

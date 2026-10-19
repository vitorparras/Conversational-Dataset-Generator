using Core.Boundary.Contracts;
using Core.Boundary.Http;

namespace Core.Boundary.Tests.Http;

/// <summary>A recognizer of the fake service, with the client the host builds it with and a captured log.</summary>
internal sealed class Recognition : IDisposable
{
    public const string Text = "falei com PESSOA_1 ontem ZQX-MARKER-TEXT-0025";

    public static readonly TimeSpan Generous = TimeSpan.FromMinutes(5);

    private readonly HttpClient client = LoopbackHttpClient.Create();

    public CapturedLog Log { get; } = new();

    public static AnalysisRequest Request => new(Text, "pt", "ZQX-REF-0025", ContractVersion.Current);

    public static AnalysisRequest RequestOfVersion(string contractVersion) => new(Text, "pt", "ZQX-REF-0025", contractVersion);

    public HttpRecognizer Of(int port, TimeSpan timeout) => new(client, new HttpRecognizerSettings(port, Reply.Secret, timeout), Log);

    public Task<RecognitionOutcome> AskAsync(int port) =>
        Of(port, Generous).RecognizeAsync(Request, TestContext.Current.CancellationToken);

    public void Dispose() => client.Dispose();
}

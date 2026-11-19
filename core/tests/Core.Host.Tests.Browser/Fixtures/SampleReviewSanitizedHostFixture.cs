using System.Diagnostics.CodeAnalysis;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>The host of the review by sample whose recognizer finds a credential in every text, so that each item of the sample is shown sanitized; started once for the tests of a class.</summary>
[SuppressMessage("Design", "CA1515", Justification = "xunit builds the class fixture and the public test classes take it in their constructors.")]
public sealed class SampleReviewSanitizedHostFixture : IAsyncLifetime
{
    private ScreenHost? host;

    internal ScreenHost Host => host ?? throw new InvalidOperationException("The host of the review by sample was not started.");

    public async ValueTask InitializeAsync() =>
        host = await ScreenHost.StartAsync(SampleReviewRealCase.Parts(new SubmissionCase.FirstCharacterCredential()), CancellationToken.None);

    public async ValueTask DisposeAsync()
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}

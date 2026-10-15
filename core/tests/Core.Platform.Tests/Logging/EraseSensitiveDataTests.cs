using Core.Platform.Logging;
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Platform.Tests.Logging;

/// <summary>Each of the three classifications is erased by what the platform registers, whatever the provider would do by default.</summary>
public sealed class EraseSensitiveDataTests
{
    private const string Marker = "ZQX-MARKER-7731";

    public static TheoryData<DataClassification> Classifications =>
        [DataClassifications.Material, DataClassifications.Identity, DataClassifications.Secret];

    [Theory]
    [MemberData(nameof(Classifications))]
    public void ClassificationIsErasedEvenWhenTheFallbackKeepsValues(DataClassification classification)
    {
        using var provider = new ServiceCollection()
            .AddRedaction(redaction => PlatformLogging.EraseSensitiveData(redaction.SetFallbackRedactor<NullRedactor>()))
            .BuildServiceProvider();

        var redacted = provider.GetRequiredService<IRedactorProvider>().GetRedactor(classification).Redact(Marker);

        Assert.Equal(string.Empty, redacted);
    }

    [Fact]
    public void UnclassifiedValueFollowsTheFallbackNotThePlatform()
    {
        using var provider = new ServiceCollection()
            .AddRedaction(redaction => PlatformLogging.EraseSensitiveData(redaction.SetFallbackRedactor<NullRedactor>()))
            .BuildServiceProvider();

        var redacted = provider.GetRequiredService<IRedactorProvider>().GetRedactor(DataClassification.Unknown).Redact(Marker);

        Assert.Equal(Marker, redacted);
    }
}

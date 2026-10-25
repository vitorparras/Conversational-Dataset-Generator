using Core.Platform.Configuration;

namespace Core.Platform.Tests;

/// <summary>The view shows every key with its origin and never a sensitive value.</summary>
public sealed class EffectiveConfigurationTests
{
    private const string Marker = "ZQX-MARKER-7731";

    [Fact]
    public void SensitiveValueIsReplacedByTheRedactionMark()
    {
        var token = new DeclaredKey("Sample:Token", ConfigurationKey.Required("Token").AsSensitive());

        var view = new EffectiveConfiguration([new ResolvedValue(token, Marker, ConfigurationOrigin.Argument)]);

        var entry = Assert.Single(view.Entries);
        Assert.Equal(EffectiveConfiguration.Redacted, entry.Value);
        Assert.DoesNotContain(Marker, entry.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void AbsentSensitiveValueIsShownAsAbsentNotAsRedacted()
    {
        var token = new DeclaredKey("Sample:Token", ConfigurationKey.Optional("Token").AsSensitive());

        var view = new EffectiveConfiguration([new ResolvedValue(token, Value: null, ConfigurationOrigin.Default)]);

        Assert.Null(Assert.Single(view.Entries).Value);
    }

    [Fact]
    public void PlainValueIsShownAsItIsWithItsOrigin()
    {
        var mode = new DeclaredKey("Sample:Mode", ConfigurationKey.Required("Mode"));

        var view = new EffectiveConfiguration([new ResolvedValue(mode, "lenient", ConfigurationOrigin.Environment)]);

        Assert.Equal(new EffectiveEntry("Sample:Mode", "lenient", ConfigurationOrigin.Environment), Assert.Single(view.Entries));
    }

    [Fact]
    public void EntriesKeepTheOrderGiven()
    {
        var first = new DeclaredKey("Sample:First", ConfigurationKey.Required("First"));
        var second = new DeclaredKey("Sample:Second", ConfigurationKey.Required("Second"));

        var view = new EffectiveConfiguration(
        [
            new ResolvedValue(first, "1", ConfigurationOrigin.File),
            new ResolvedValue(second, "2", ConfigurationOrigin.Default),
        ]);

        Assert.Equal(["Sample:First", "Sample:Second"], view.Entries.Select(entry => entry.Key));
    }
}

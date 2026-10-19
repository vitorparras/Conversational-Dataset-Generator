using Core.Platform.Configuration;
using Core.Tests.Properties.Harness;
using CsCheck;

namespace Core.Tests.Properties.Configuration;

/// <summary>Loading keeps the invariants of requisito 032 (docs/requisitos/032-configuracao-falha-fechada.md); a load that breaks one falls to its property.</summary>
public sealed class ClosedConfigurationInvariantsTests
{
    private static ClosedConfigurationInvariants Over(ConfigurationLoader load) => new(PropertyRunner.FromProcess(), load);

    private static ConfigurationLoadResult Production(ConfigurationSchema schema, IEnumerable<IConfigurationSource> sources) =>
        ClosedConfiguration.Load(schema, sources);

    [Fact]
    [Trait("Kind", "Property")]
    public void UnknownKeyIsRefusedAndNamed() => Over(Production).UnknownKeyIsRefusedAndNamed();

    [Fact]
    [Trait("Kind", "Property")]
    public void MissingRequiredKeyIsRefusedAndNamed() => Over(Production).MissingRequiredKeyIsRefusedAndNamed();

    [Fact]
    [Trait("Kind", "Property")]
    public void LoadedValuesShowTheirOrigin() => Over(Production).LoadedValuesShowTheirOrigin();

    [Fact]
    public void ALoadThatIgnoresUnknownKeysFailsTheUnknownKeyProperty() =>
        Assert.Throws<CsCheckException>(Over(BrokenLoaders.IgnoringUnknownKeys).UnknownKeyIsRefusedAndNamed);

    [Fact]
    public void ALoadThatFillsMissingKeysFailsTheMissingKeyProperty() =>
        Assert.Throws<CsCheckException>(Over(BrokenLoaders.FillingMissingKeys).MissingRequiredKeyIsRefusedAndNamed);

    [Fact]
    public void ALoadThatHidesOriginsFailsTheOriginProperty() =>
        Assert.Throws<CsCheckException>(Over(BrokenLoaders.HidingOrigins).LoadedValuesShowTheirOrigin);
}

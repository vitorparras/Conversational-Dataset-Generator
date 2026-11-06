namespace Core.Tests.Architecture.CodeRules;

/// <summary>The exception for the screens (decision 0052) is narrow: only the Razor generator, only the components directly under the screens, only its two warnings.</summary>
public sealed class RazorScreensTests
{
    private const string Generated = "Microsoft.CodeAnalysis.Razor.Compiler/Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator/";
    private const string Body = "#pragma warning disable 1591\nnamespace Core.Host.Screens { }\n";

    [Theory]
    [InlineData(Generated + "Screens/ScreenFrame_razor.g.cs")]
    [InlineData(@"Microsoft.CodeAnalysis.Razor.Compiler\Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator\Screens\ScreenFrame_razor.g.cs")]
    public void TheDisableTheRazorGeneratorWritesForAScreenIsAccepted(string path) =>
        Assert.Empty(ProductionSuppressionRules.CheckSources([SourceFile.FromText(path, Body)]));

    [Theory]
    [InlineData(Generated + "Endpoints/StatusEndpoint_razor.g.cs", Body)]
    [InlineData(Generated + "Screens/Inner/Frame_razor.g.cs", Body)]
    [InlineData(Generated + @"Screens\Inner\Frame_razor.g.cs", Body)]
    [InlineData("Other.Generator/Screens/ScreenFrame_razor.g.cs", Body)]
    [InlineData("src/Core.Host/Screens/ScreenAssets.cs", Body)]
    [InlineData(Generated + "Screens/ScreenFrame_razor.g.cs", "#pragma warning disable S1144\nnamespace Core.Host.Screens { }\n")]
    [InlineData(Generated + "Screens/ScreenFrame_razor.g.cs", "#pragma warning disable\nnamespace Core.Host.Screens { }\n")]
    public void TheSameDisableAnywhereElseOrForAnythingElseIsRefused(string path, string text) =>
        Assert.NotEmpty(ProductionSuppressionRules.CheckSources([SourceFile.FromText(path, text)]));

    [Fact]
    public void OnlyAComponentDirectlyUnderTheScreensIsAnAcceptedAdditionalFile()
    {
        var screens = Path.Combine(SourceTree.CoreRoot, "src", "Core.Host", "Screens");

        Assert.True(RazorScreens.IsComponent(Path.Combine(screens, "ScreenFrame.razor")));
        Assert.False(RazorScreens.IsComponent(Path.Combine(screens, "Inner", "Frame.razor")));
        Assert.False(RazorScreens.IsComponent(Path.Combine(screens, "notes.txt")));
        Assert.False(RazorScreens.IsComponent(Path.Combine(SourceTree.CoreRoot, "src", "Core.Host", "Endpoints", "Frame.razor")));
    }
}

using Core.Tests.Browser.Accessibility;
using Core.Tests.Browser.Fixtures;
using Core.Tests.Browser.Trial;

namespace Core.Tests.Browser.Screens;

/// <summary>
/// The glass of the design system (issue 658, CA-3 and CA-5): translucent and blurred by the style alone in the usual setting, a solid surface without blur when the person asks for less
/// transparency or more contrast, and a dark theme that the system chooses. The surfaces are found by what they do, and not by the markup of a component.
/// </summary>
[Trait("Category", "Browser")]
public sealed class GlassTests(BrowserFixture browser)
{
    private const string Helpers = """
        const alphaOf = (c) => { const m = c.match(/[0-9.]+/g); return m.length > 3 ? Number(m[3]) : 1; };
        const blurOf = (s) => { const m = s.backdropFilter.match(/blur[(]([0-9.]+)px/); return m ? Number(m[1]) : 0; };
        """;

    private const string MarkGlass = $$"""
        () => {
          {{Helpers}}
          const found = [...document.querySelectorAll('*')].filter((e) => blurOf(getComputedStyle(e)) > 0);
          found.forEach((e) => e.setAttribute('data-probe-glass', ''));
          return found.map((e) => alphaOf(getComputedStyle(e).backgroundColor));
        }
        """;

    private const string NotSolid = $$"""
        () => {
          {{Helpers}}
          return [...document.querySelectorAll('[data-probe-glass]')].map((e) => ({ e, s: getComputedStyle(e) }))
            .filter(({ s }) => blurOf(s) > 0 || alphaOf(s.backgroundColor) !== 1 || s.backgroundImage !== 'none')
            .map(({ e, s }) => `${e.tagName.toLowerCase()}.${e.className} backdrop-filter=${s.backdropFilter} background=${s.backgroundColor} ${s.backgroundImage}`);
        }
        """;

    private const string Palette = "() => getComputedStyle(document.body).backgroundColor + ' / ' + getComputedStyle(document.body).color";

    [Fact]
    public async Task Ca3TheGlassIsTranslucentAndBlurredInTheUsualSetting()
    {
        await using var tab = await browser.OpenAsync(TrialRoutes.Catalog);

        var alphas = await tab.Page.EvaluateAsync<double[]>(MarkGlass);

        Assert.NotEmpty(alphas);
        Assert.DoesNotContain(alphas, alpha => alpha >= 1);
    }

    [Theory]
    [InlineData("ReducedTransparency")]
    [InlineData("HighContrast")]
    public async Task Ca3TheGlassBecomesASolidSurfaceWhenThePersonAsksForLessTransparencyOrMoreContrast(string name)
    {
        await using var tab = await browser.OpenAsync(TrialRoutes.Catalog);
        Assert.NotEmpty(await tab.Page.EvaluateAsync<double[]>(MarkGlass));

        await MediaMode.Named(name).ApplyAsync(tab.Page);

        Assert.Empty(await tab.Page.EvaluateAsync<string[]>(NotSolid));
    }

    [Fact]
    public async Task Ca3TheGlassComesBackWhenTheSettingGoesBackToTheUsualOne()
    {
        await using var tab = await browser.OpenAsync(TrialRoutes.Catalog);
        var glass = (await tab.Page.EvaluateAsync<double[]>(MarkGlass)).Length;
        await MediaMode.ReducedTransparency.ApplyAsync(tab.Page);
        await MediaMode.Light.ApplyAsync(tab.Page);

        var translucent = await tab.Page.EvaluateAsync<string[]>(NotSolid);

        Assert.Equal(glass, translucent.Length);
    }

    [Fact]
    public async Task Ca3TheDarkSchemeOfTheSystemChangesTheColoursOfThePage()
    {
        await using var tab = await browser.OpenAsync(TrialRoutes.Catalog);
        var light = await tab.Page.EvaluateAsync<string>(Palette);

        await MediaMode.Dark.ApplyAsync(tab.Page);

        Assert.NotEqual(light, await tab.Page.EvaluateAsync<string>(Palette));
    }
}

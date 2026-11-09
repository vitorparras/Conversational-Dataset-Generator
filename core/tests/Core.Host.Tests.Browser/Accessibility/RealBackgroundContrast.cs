using Microsoft.Playwright;

namespace Core.Host.Tests.Browser.Accessibility;

/// <summary>
/// The contrast of every text of the page measured over the background as it is drawn (issue 658, CA-3). A tool that reads the style cannot see through a glass that blurs and lets
/// the background pass; so the text is hidden, the page is photographed, and the colour of the text is held against the darkest and the lightest pixel under it. Normal text needs 4.5,
/// large text 3 (WCAG 2.1, 1.4.3), and the transparency of the text itself and of the elements around it is counted. Text off the page and text of a disabled control are left out,
/// as the criterion leaves out the inactive component, and text that another element covers or that is clipped to a pixel for readers of screens alone is not read by anyone;
/// the link to skip to the content is measured with the focus on it.
/// </summary>
internal static class RealBackgroundContrast
{
    /// <summary>The colours are read once the transitions that a change of setting starts are over, or they would be the ones half way.</summary>
    private const string Settled = "() => Promise.all(document.getAnimations().map(animation => animation.finished))";

    private const string Collect = """
        () => {
          const channels = (c) => c.match(/[0-9.]+/g).map(Number);
          const runs = [];
          const opacityOf = (e) => { let o = 1; for (; e; e = e.parentElement) { o *= Number(getComputedStyle(e).opacity); } return o; };
          const clippedAway = (e) => {
            for (; e; e = e.parentElement) {
              const box = e.getBoundingClientRect();
              if (getComputedStyle(e).clipPath !== 'none' && box.width <= 1 && box.height <= 1) { return true; }
            }
            return false;
          };
          const covered = (e, rect) => {
            const hit = document.elementFromPoint(rect.left + rect.width / 2, rect.top + rect.height / 2);
            return hit !== null && !e.contains(hit) && !hit.contains(e);
          };
          const add = (e, text, rect, color) => {
            if (covered(e, rect)) { return; }
            const style = getComputedStyle(e);
            const c = channels(color);
            const size = parseFloat(style.fontSize);
            runs.push({ text: text.slice(0, 40), x: rect.left + scrollX, y: rect.top + scrollY, w: rect.width, h: rect.height,
              r: c[0], g: c[1], b: c[2], alpha: (c.length > 3 ? c[3] : 1) * opacityOf(e),
              large: size >= 24 || (size >= 18.66 && Number(style.fontWeight) >= 700) });
          };
          const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
          for (let node = walker.nextNode(); node; node = walker.nextNode()) {
            const e = node.parentElement;
            if (!node.textContent.trim() || !e.checkVisibility() || clippedAway(e) || e.closest('script,style,noscript,option,:disabled,[aria-disabled=true]')) { continue; }
            const range = document.createRange();
            range.selectNodeContents(node);
            for (const rect of range.getClientRects()) {
              if (rect.width >= 1 && rect.height >= 1 && rect.right > 0 && rect.bottom > 0) { add(e, node.textContent.trim(), rect, getComputedStyle(e).color); }
            }
          }
          for (const e of document.querySelectorAll('input:not([type=hidden],[type=radio],[type=checkbox],[type=file],[type=range],[type=color],[type=image]),textarea')) {
            const shown = e.value || e.placeholder;
            if (!shown || !e.checkVisibility()) { continue; }
            const s = getComputedStyle(e);
            const r = e.getBoundingClientRect();
            const inset = (side) => parseFloat(s['border' + side + 'Width']) + parseFloat(s['padding' + side]);
            const rect = { left: r.left + inset('Left'), top: r.top + inset('Top'), width: r.width - inset('Left') - inset('Right'), height: r.height - inset('Top') - inset('Bottom') };
            add(e, shown, rect, e.value ? s.color : getComputedStyle(e, '::placeholder').color);
          }
          window.textRuns = runs;
          return runs.length;
        }
        """;

    private const string Measure = """
        async (png) => {
          const bytes = Uint8Array.from(atob(png), (c) => c.charCodeAt(0));
          const bitmap = await createImageBitmap(new Blob([bytes], { type: 'image/png' }));
          const context = new OffscreenCanvas(bitmap.width, bitmap.height).getContext('2d', { willReadFrequently: true });
          context.drawImage(bitmap, 0, 0);
          const linear = (v) => { v /= 255; return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4); };
          const luminance = (p) => 0.2126 * linear(p[0]) + 0.7152 * linear(p[1]) + 0.0722 * linear(p[2]);
          const ratio = (a, b) => (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05);
          const failures = [];
          for (const run of window.textRuns) {
            const x = Math.max(0, Math.floor(run.x * devicePixelRatio));
            const y = Math.max(0, Math.floor(run.y * devicePixelRatio));
            const w = Math.min(bitmap.width - x, Math.ceil(run.w * devicePixelRatio));
            const h = Math.min(bitmap.height - y, Math.ceil(run.h * devicePixelRatio));
            if (w < 1 || h < 1) { continue; }
            const data = context.getImageData(x, y, w, h).data;
            let darkest = [0, 0, 0], lightest = [0, 0, 0], low = 2, high = -1;
            for (let i = 0; i < data.length; i += 4) {
              const pixel = [data[i], data[i + 1], data[i + 2]];
              const l = luminance(pixel);
              if (l < low) { low = l; darkest = pixel; }
              if (l > high) { high = l; lightest = pixel; }
            }
            const over = (bg) => luminance([run.r, run.g, run.b].map((v, i) => v * run.alpha + bg[i] * (1 - run.alpha)));
            const worst = Math.min(ratio(over(darkest), low), ratio(over(lightest), high));
            const need = run.large ? 3 : 4.5;
            if (worst < need) { failures.push(`${run.text} (${worst.toFixed(2)} of ${need})`); }
          }
          return failures;
        }
        """;

    private const string HideText = "*, *::placeholder { color: transparent !important; -webkit-text-fill-color: transparent !important; text-shadow: none !important; "
        + "text-decoration-color: transparent !important; caret-color: transparent !important; }";

    /// <summary>Fails with the list of the texts under the ratio they need. The page is as it was after the call, apart from the animations the photograph stopped.</summary>
    public static async Task AssertAaAsync(IPage page)
    {
        _ = await page.EvaluateAsync<object>(Settled);
        var count = await page.EvaluateAsync<int>(Collect);
        Assert.True(count > 0, "The page shows no text to measure.");
        var hide = await page.AddStyleTagAsync(new PageAddStyleTagOptions { Content = HideText });
        try
        {
            var photograph = await page.ScreenshotAsync(new PageScreenshotOptions { FullPage = true, Animations = ScreenshotAnimations.Disabled });
            Assert.Empty(await page.EvaluateAsync<string[]>(Measure, Convert.ToBase64String(photograph)));
        }
        finally
        {
            _ = await hide.EvaluateAsync<object>("element => element.remove()");
        }
    }
}

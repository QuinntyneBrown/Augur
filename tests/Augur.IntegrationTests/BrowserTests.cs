using System.Text.Json;
using Augur.IntegrationTests.Support;
using Augur.Tests.Support;
using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;

namespace Augur.IntegrationTests;

/// <summary>
/// Drives emitted Angular apps in a real browser to check what file inspection cannot: responsive navigation (L2-055),
/// accessibility (L2-056), the sign-in redirect (L2-054), and the API status on the home page (L2-053).
/// Lighthouse timings (L2-057 AC4) remain a manual release check.
/// </summary>
public sealed class BrowserTests
{
    private static readonly int[] Widths = [320, 375, 576, 768, 992, 1200, 1920, 2560];
    private static readonly string[] WcagTags = ["wcag2a", "wcag2aa", "wcag21aa", "wcag22aa"];

    [SlowFact]
    public Task The_plain_shell_is_responsive_accessible_and_signs_in_with_pkce() => VerifyShellAsync("none");

    [SlowFact]
    public Task The_material_shell_is_responsive_accessible_and_signs_in_with_pkce() => VerifyShellAsync("angular-material");

    [SlowFact]
    public async Task The_fullstack_home_page_shows_whether_the_api_is_up()
    {
        using var build = new EmittedBuild("browser-fullstack");
        var plan = Plans.Fullstack("minimal-api", "none", authentication: false, worker: false, cqrs: null, "none", "signals", ssr: false);
        var name = (await build.EmitAsync("Shop", [plan])).Single();
        var publish = Path.Combine(build.Root, "publish");
        var api = Path.Combine(build.Root, name, $"{name}.Api", $"{name}.Api.csproj");
        var published = await EmittedBuild.RunAsync("dotnet", $"publish \"{api}\" -c Release -o \"{publish}\"", build.Root, TimeSpan.FromMinutes(15));
        Assert.True(published.ExitCode == 0, published.Output);

        await using var browser = await Browser.LaunchAsync();
        await using (var running = await PublishedApi.StartAsync(publish, $"{name}.Api.dll"))
        {
            var page = await browser.OpenAsync(running.Url, 1200);
            await Assertions.Expect(page.Page.GetByRole(AriaRole.Status)).ToHaveTextAsync("API: Healthy");
            Assert.Empty(page.Errors);
        }

        await using var withoutApi = await StaticSite.StartAsync(Path.Combine(publish, "wwwroot"));
        var stopped = await browser.OpenAsync(withoutApi.Url, 1200);
        await Assertions.Expect(stopped.Page.GetByRole(AriaRole.Status)).ToHaveTextAsync("API: Unavailable", new() { Timeout = 5000 });
        Assert.Empty(stopped.Errors);
    }

    private static async Task VerifyShellAsync(string uiLibrary)
    {
        await using var authority = await StubAuthority.StartAsync();
        using var build = new EmittedBuild($"browser-{uiLibrary}");
        var name = (await build.EmitAsync("Shop", [Plans.Angular(uiLibrary, "signals", ssr: false, authentication: true)])).Single();
        var workspace = Path.Combine(build.Root, name);
        var environment = Path.Combine(workspace, "src", "environments", "environment.ts");
        File.WriteAllText(environment, File.ReadAllText(environment)
            .Replace("authority: ''", $"authority: '{authority.Url}'", StringComparison.Ordinal)
            .Replace("clientId: ''", "clientId: 'augur-tests'", StringComparison.Ordinal));
        var dist = await Npm.InstallAndBuildAsync(workspace);

        await using var site = await StaticSite.StartAsync(dist);
        await using var browser = await Browser.LaunchAsync();
        var failures = new List<string>();
        void Check(bool condition, string failure)
        {
            if (!condition)
            {
                failures.Add(failure);
            }
        }

        // L2-055 AC1: below 768px the navigation hides behind the menu button and collapses after navigating.
        var narrow = await browser.OpenAsync(site.Url, 375);
        var home = NavLink(narrow.Page, "Home");
        var menu = narrow.Page.Locator(".menu-button");
        Check(await menu.IsVisibleAsync(), "375px: the menu button is not visible");
        Check(!await home.IsVisibleAsync(), "375px: navigation links are visible before the menu is opened");
        await menu.ClickAsync();
        await Assertions.Expect(home).ToBeVisibleAsync();
        await NavLink(narrow.Page, "Sign in").ClickAsync();
        await Assertions.Expect(narrow.Page).ToHaveURLAsync(SignInUrl());
        await Assertions.Expect(home).ToBeHiddenAsync();

        // L2-055 AC5: every visible interactive element is at least 44x44 at 375px.
        var narrowHome = await browser.OpenAsync(site.Url, 375);
        foreach (var size in await InteractiveSizesAsync(narrowHome.Page))
        {
            Check(size.Width >= 44 && size.Height >= 44, $"375px: '{size.Name}' is {size.Width:0}x{size.Height:0}");
        }

        // L2-055 AC2-3: collapsed at 576px, open without interaction from 768px.
        var small = await browser.OpenAsync(site.Url, 576);
        Check(!await NavLink(small.Page, "Home").IsVisibleAsync(), "576px: navigation is not collapsed");
        foreach (var width in new[] { 768, 992, 1200, 1920 })
        {
            var wide = await browser.OpenAsync(site.Url, width);
            Check(await NavLink(wide.Page, "Home").IsVisibleAsync(), $"{width}px: navigation is not visible");
            Check(!await wide.Page.Locator(".menu-button").IsVisibleAsync(), $"{width}px: the menu button is shown");
        }

        // L2-055 AC4 and AC6, L2-052 AC6: no horizontal scrolling and readable text at every width, on home and not-found.
        foreach (var width in Widths)
        {
            foreach (var path in new[] { "/", "/does-not-exist" })
            {
                var page = await browser.OpenAsync(site.Url + path, width);
                Check(await page.Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= window.innerWidth"), $"{width}px {path}: scrolls horizontally");
                var fontSize = await page.Page.EvaluateAsync<double>("() => parseFloat(getComputedStyle(document.querySelector('main p')).fontSize)");
                Check(fontSize >= 16, $"{width}px {path}: main text is {fontSize}px");
                if (path == "/does-not-exist")
                {
                    Check(await page.Page.Locator("main h1").InnerTextAsync() == "Page not found", $"{width}px: the not-found page is not shown");
                }
            }
        }

        // L2-056 AC1: no WCAG 2.2 AA violations on any page at 375px and 1200px.
        foreach (var width in new[] { 375, 1200 })
        {
            foreach (var path in new[] { "/", "/does-not-exist", "/sign-in", "/sign-out" })
            {
                var page = await browser.OpenAsync(site.Url + path, width);
                var result = await page.Page.RunAxe(new AxeRunOptions { RunOnly = new RunOnlyOptions { Type = "tag", Values = [.. WcagTags] } });
                foreach (var violation in result.Violations)
                {
                    failures.Add($"axe {width}px {path}: {violation.Id}: {violation.Help} ({string.Join(", ", violation.Nodes.Select(n => string.Join(' ', n.Target)))})");
                }
            }
        }

        // L2-056 AC2: the first Tab reaches a visible skip link that moves focus to main.
        var keyboard = await browser.OpenAsync(site.Url, 1200);
        await keyboard.Page.Keyboard.PressAsync("Tab");
        Check(await keyboard.Page.EvaluateAsync<string>("() => document.activeElement.textContent.trim()") == "Skip to main content", "Tab does not reach the skip link first");
        var skip = await keyboard.Page.Locator(".skip-link").BoundingBoxAsync();
        Check(skip is { Y: >= 0 }, "the focused skip link is not on screen");
        await keyboard.Page.Keyboard.PressAsync("Enter");
        Check(await keyboard.Page.EvaluateAsync<string>("() => document.activeElement.id") == "main", "the skip link does not move focus to main");

        // L2-056 AC3: every interactive element is reachable with Tab and shows a focus indicator.
        var tabbing = await browser.OpenAsync(site.Url, 1200);
        var interactive = await tabbing.Page.EvaluateAsync<int>("() => [...document.querySelectorAll('a[href], button')].filter(e => e.offsetParent !== null || e.classList.contains('skip-link')).length");
        var reached = new HashSet<string>();
        for (var i = 0; i < interactive + 1; i++)
        {
            await tabbing.Page.Keyboard.PressAsync("Tab");
            var focused = await tabbing.Page.EvaluateAsync<JsonElement>(
                "() => { const e = document.activeElement; const s = getComputedStyle(e); return { name: e === document.body || e === document.documentElement ? 'BODY' : (e.textContent.trim() || e.getAttribute('aria-label') || e.tagName), outline: s.outlineStyle, width: parseFloat(s.outlineWidth) }; }");
            var focusedName = focused.GetProperty("name").GetString()!;
            if (focusedName is "BODY")
            {
                continue;
            }

            reached.Add(focusedName);
            Check(focused.GetProperty("outline").GetString() != "none" && focused.GetProperty("width").GetDouble() > 0, $"'{focusedName}' shows no focus indicator");
        }

        Check(reached.Count >= interactive, $"Tab reached {reached.Count} of {interactive} interactive elements: {string.Join(", ", reached)}");

        // L2-056 AC4: one main, one banner, one navigation landmark.
        var landmarks = await browser.OpenAsync(site.Url, 1200);
        Check(await landmarks.Page.GetByRole(AriaRole.Main).CountAsync() == 1, "there is not exactly one main landmark");
        Check(await landmarks.Page.GetByRole(AriaRole.Banner).CountAsync() == 1, "there is not exactly one banner landmark");
        Check(await landmarks.Page.GetByRole(AriaRole.Navigation).CountAsync() == 1, "there is not exactly one navigation landmark");

        // L2-056 AC5: each route has its own title, and focus moves to its heading after navigation.
        var titles = await browser.OpenAsync(site.Url, 1200);
        var homeTitle = await titles.Page.TitleAsync();
        await NavLink(titles.Page, "Sign in").ClickAsync();
        await Assertions.Expect(titles.Page).ToHaveURLAsync(SignInUrl());
        await Assertions.Expect(titles.Page.Locator("main h1")).ToBeFocusedAsync();
        Check(await titles.Page.TitleAsync() != homeTitle, "the sign-in page has the same title as home");

        // L2-056 AC6: with reduced motion, opening and closing the plain navigation runs no transition.
        if (uiLibrary == "none")
        {
            var reduced = await browser.OpenAsync(site.Url, 375, reduceMotion: true);
            await reduced.Page.Locator(".menu-button").ClickAsync();
            var duration = await reduced.Page.EvaluateAsync<string>("() => getComputedStyle(document.querySelector('#main-nav a')).transitionDuration");
            Check(duration.Split(',').All(d => d.Trim() == "0s"), $"reduced motion still transitions: {duration}");
        }

        // L2-054 AC4: a protected page redirects to the authority with PKCE before it renders.
        var notesRendered = false;
        var protectedPage = await browser.OpenBlankAsync(1200);
        await protectedPage.Page.ExposeFunctionAsync("augurNotesRendered", () => notesRendered = true);
        await protectedPage.Page.AddInitScriptAsync(
            "new MutationObserver(() => { if ([...document.querySelectorAll('h1')].some(h => h.textContent.trim() === 'Notes')) { window.augurNotesRendered(); } })"
            + ".observe(document, { subtree: true, childList: true });");
        await protectedPage.Page.GotoAsync(site.Url + "/notes");
        await protectedPage.Page.WaitForURLAsync($"{authority.Url}/authorize**");
        var query = System.Web.HttpUtility.ParseQueryString(new Uri(protectedPage.Page.Url).Query);
        Check(query["response_type"] == "code", $"response_type is {query["response_type"]}");
        Check(query["code_challenge_method"] == "S256", $"code_challenge_method is {query["code_challenge_method"]}");
        Check(!string.IsNullOrEmpty(query["code_challenge"]), "no code_challenge");
        Check(query["client_secret"] is null, "a client secret was sent");
        Check(!notesRendered, "the protected page rendered before sign-in");

        // L2-054 AC6: nothing is kept in localStorage.
        var storage = await browser.OpenAsync(site.Url, 1200);
        Check(await storage.Page.EvaluateAsync<int>("() => localStorage.length") == 0, "localStorage is not empty");

        // L2-053 AC4 spirit for every page: no uncaught errors in the browser.
        failures.AddRange(browser.AllErrors.Select(e => $"uncaught error: {e}"));

        Assert.True(failures.Count == 0, $"{uiLibrary} shell ({build.Root}):\n{string.Join('\n', failures)}");
    }

    /// <summary>In-app navigation changes the URL without a load event, so tests poll the URL instead of waiting for a load.</summary>
    private static System.Text.RegularExpressions.Regex SignInUrl() => new("/sign-in$");

    private static ILocator NavLink(IPage page, string name) =>
        page.Locator("#main-nav a", new() { HasTextString = name });

    private static async Task<List<(string Name, double Width, double Height)>> InteractiveSizesAsync(IPage page)
    {
        var sizes = new List<(string, double, double)>();
        foreach (var element in await page.Locator("a[href], button").AllAsync())
        {
            if (!await element.IsVisibleAsync())
            {
                continue;
            }

            if (await element.BoundingBoxAsync() is { } box)
            {
                var name = (await element.InnerTextAsync()).Trim();
                sizes.Add((name.Length > 0 ? name : (await element.GetAttributeAsync("aria-label")) ?? "?", box.Width, box.Height));
            }
        }

        return sizes;
    }
}

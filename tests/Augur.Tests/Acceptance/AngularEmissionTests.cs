using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Augur.Tests.Support;

namespace Augur.Tests.Acceptance;

public sealed class AngularEmissionTests
{
    [Theory]
    [InlineData("Contoso.Orders", "contoso-orders")]
    [InlineData("Contoso.OrderPortal", "contoso-order-portal")]
    [InlineData("Acme.HRPortal", "acme-hr-portal")]
    [InlineData("Contoso2Web", "contoso2-web")]
    public async Task The_angular_project_is_named_from_the_solution_name(string solutionName, string projectName)
    {
        var files = await Emit(Plans.Angular("none", "signals", ssr: false, authentication: false), solutionName);

        var angular = JsonNode.Parse(files["angular.json"])!;
        Assert.NotNull(angular["projects"]![projectName]);
        Assert.Equal(projectName, JsonNode.Parse(files["package.json"])!["name"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_plain_signals_workspace_has_no_material_or_ngrx()
    {
        var files = await Emit(Plans.Angular("none", "signals", ssr: false, authentication: false));

        Assert.Contains("angular.json", files.Keys);
        var dependencies = AllDependencies(files["package.json"]);
        Assert.DoesNotContain("@angular/material", dependencies.Keys);
        Assert.DoesNotContain(dependencies.Keys, d => d.StartsWith("@ngrx/", StringComparison.Ordinal));
        Assert.Contains("src/app/state/app-state.ts", files.Keys);
    }

    [Fact]
    public async Task Material_and_the_signal_store_are_pinned_exactly()
    {
        var files = await Emit(Plans.Angular("angular-material", "ngrx-signal-store", ssr: false, authentication: false));

        var dependencies = AllDependencies(files["package.json"]);
        Assert.Matches(@"^\d+\.\d+\.\d+$", dependencies["@angular/material"]);
        Assert.Matches(@"^\d+\.\d+\.\d+$", dependencies["@ngrx/signals"]);
        Assert.Contains("signalStore(", files["src/app/state/app-store.ts"]);
        Assert.Contains("<mat-sidenav", files["src/app/app.html"]);
        Assert.Contains("<mat-toolbar", files["src/app/app.html"]);
    }

    [Fact]
    public async Task Server_side_rendering_configures_angular_ssr()
    {
        var files = await Emit(Plans.Angular("none", "signals", ssr: true, authentication: false));

        Assert.Contains("@angular/ssr", AllDependencies(files["package.json"]).Keys);
        var build = JsonNode.Parse(files["angular.json"])!["projects"]!["contoso-orders"]!["architect"]!["build"]!["options"]!;
        Assert.Equal("src/main.server.ts", build["server"]!.GetValue<string>());
        Assert.Contains("src/server.ts", files.Keys);
    }

    [Fact]
    public async Task Authentication_adds_sign_in_sign_out_a_guard_and_an_interceptor()
    {
        var files = await Emit(Plans.Angular("none", "signals", ssr: false, authentication: true));

        Assert.Contains("src/app/auth/sign-in.ts", files.Keys);
        Assert.Contains("src/app/auth/sign-out.ts", files.Keys);
        Assert.Contains("canActivate: [authGuard]", files["src/app/app.routes.ts"]);
        Assert.Contains("withInterceptors([authInterceptor])", files["src/app/app.config.ts"]);
        Assert.Contains("responseType: 'code'", files["src/app/auth/auth.service.ts"]);
    }

    [Fact]
    public async Task Every_angular_combination_meets_the_structure_security_and_budget_rules()
    {
        var plans = Plans.AllAngular().ToList();
        Assert.Equal(16, plans.Count);
        foreach (var decisions in plans)
        {
            var files = await Emit(decisions);
            var label = Plans.Describe(decisions);
            var source = string.Join('\n', files.Where(f => f.Key.StartsWith("src/", StringComparison.Ordinal)).Select(f => f.Value));

            Assert.All(AllDependencies(files["package.json"]), d => Assert.Matches(@"^\d+\.\d+\.\d+$", d.Value));
            Assert.Equal("^20.19.0 || ^22.12.0 || >=24.0.0", JsonNode.Parse(files["package.json"])!["engines"]!["node"]!.GetValue<string>());
            Assert.False(source.Contains("@NgModule", StringComparison.Ordinal), $"{label}: uses an NgModule");
            Assert.False(Regex.IsMatch(source, @"\[innerHTML\]|bypassSecurityTrust"), $"{label}: bypasses sanitization");
            Assert.False(Regex.IsMatch(StripComments(source), @"localStorage"), $"{label}: uses localStorage");
            Assert.DoesNotMatch(@"(?i)client_?secret|-----BEGIN [A-Z ]*PRIVATE KEY-----|password\s*[:=]", string.Join('\n', files.Values));

            var tsconfig = JsonNode.Parse(StripComments(files["tsconfig.json"]))!;
            Assert.True(tsconfig["compilerOptions"]!["strict"]!.GetValue<bool>());
            Assert.True(tsconfig["angularCompilerOptions"]!["strictTemplates"]!.GetValue<bool>());

            var production = JsonNode.Parse(files["angular.json"])!["projects"]!["contoso-orders"]!["architect"]!["build"]!["configurations"]!["production"]!;
            var budgets = production["budgets"]!.AsArray();
            Assert.Contains(budgets, b => b!["type"]!.GetValue<string>() == "initial" && b["maximumWarning"]!.GetValue<string>() == "500kB" && b["maximumError"]!.GetValue<string>() == "1MB");
            Assert.Contains(budgets, b => b!["type"]!.GetValue<string>() == "anyComponentStyle" && b["maximumWarning"]!.GetValue<string>() == "4kB" && b["maximumError"]!.GetValue<string>() == "8kB");

            Assert.Contains("loadComponent: () => import('./pages/not-found/not-found')", files["src/app/app.routes.ts"]);
            Assert.Contains("package-lock.json", files.Keys);
            Assert.Equal("contoso-orders", JsonNode.Parse(files["package-lock.json"])!["name"]!.GetValue<string>());
            Assert.Contains(".editorconfig", files.Keys);
            Assert.Contains(".gitignore", files.Keys);
        }
    }

    [Fact]
    public async Task A_fullstack_plan_puts_the_solution_at_the_root_and_the_workspace_in_web()
    {
        var files = await Emit(Plans.Fullstack("minimal-api", "none", authentication: false, worker: false, cqrs: null, "none", "signals", ssr: false));

        Assert.Contains("Contoso.Orders.slnx", files.Keys);
        Assert.Contains("web/angular.json", files.Keys);
        Assert.Equal("proxy.conf.json", JsonNode.Parse(files["web/angular.json"])!["projects"]!["contoso-orders"]!["architect"]!["serve"]!["options"]!["proxyConfig"]!.GetValue<string>());
        Assert.Contains("\"/api\"", files["web/proxy.conf.json"]);
        Assert.Contains("apiBaseUrl: '/api'", files["web/src/environments/environment.ts"]);
        Assert.Contains("API: {{ status() }}", files["web/src/app/pages/home/home.ts"]);
        Assert.Contains("BuildAngular", files["Contoso.Orders.Api/Contoso.Orders.Api.csproj"]);
        Assert.Contains("MapFallbackToFile", files["Contoso.Orders.Api/SpaStartup.cs"]);
        Assert.DoesNotContain("Contoso.Orders.Api/CorsStartup.cs", files.Keys);
    }

    [Fact]
    public async Task The_proxy_targets_the_port_in_the_api_launch_profile()
    {
        var files = await Emit(Plans.Fullstack("minimal-api", "none", authentication: false, worker: false, cqrs: null, "none", "signals", ssr: false));

        var target = JsonNode.Parse(files["web/proxy.conf.json"])!["/api"]!["target"]!.GetValue<string>();
        var launch = JsonNode.Parse(files["Contoso.Orders.Api/Properties/launchSettings.json"])!["profiles"]!["https"]!["applicationUrl"]!.GetValue<string>();
        Assert.StartsWith(target + ";", launch);
    }

    [Theory]
    [InlineData("plain-signals", "none", "signals", false, false)]
    [InlineData("material-store-ssr-auth", "angular-material", "ngrx-signal-store", true, true)]
    public async Task Emitted_workspaces_match_their_golden_copies(string name, string uiLibrary, string state, bool ssr, bool authentication)
    {
        var files = await DotNetEmissionTests.EmitBytes(Plans.Angular(uiLibrary, state, ssr, authentication));
        files.Remove("package-lock.json");

        Golden.Compare($"angular/{name}", files);
    }

    private static Task<SortedDictionary<string, string>> Emit(IReadOnlyDictionary<string, object> decisions, string name = "Contoso.Orders") =>
        EmitNamed(decisions, name);

    private static async Task<SortedDictionary<string, string>> EmitNamed(IReadOnlyDictionary<string, object> decisions, string name) =>
        new((await DotNetEmissionTests.EmitBytes(decisions, name)).ToDictionary(f => f.Key, f => System.Text.Encoding.UTF8.GetString(f.Value)), StringComparer.Ordinal);

    private static Dictionary<string, string> AllDependencies(string packageJson)
    {
        var json = JsonNode.Parse(packageJson)!;
        return new[] { "dependencies", "devDependencies" }
            .SelectMany(section => json[section]?.AsObject() ?? [])
            .ToDictionary(d => d.Key, d => d.Value!.GetValue<string>(), StringComparer.Ordinal);
    }

    private static string StripComments(string code) =>
        Regex.Replace(Regex.Replace(code, @"/\*.*?\*/", "", RegexOptions.Singleline), @"(?m)^\s*//.*$", "");
}

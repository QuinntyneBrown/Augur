namespace Augur.Emission.Angular;

/// <summary>Exact versions of every npm package an emitted workspace uses, all from the pinned Angular 21 release line.</summary>
public static class NpmPackages
{
    public const string NodeEngines = "^20.19.0 || ^22.12.0 || >=24.0.0";

    public static readonly IReadOnlyDictionary<string, string> Versions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["@angular/common"] = "21.2.25",
        ["@angular/compiler"] = "21.2.25",
        ["@angular/core"] = "21.2.25",
        ["@angular/forms"] = "21.2.25",
        ["@angular/platform-browser"] = "21.2.25",
        ["@angular/platform-server"] = "21.2.25",
        ["@angular/router"] = "21.2.25",
        ["@angular/compiler-cli"] = "21.2.25",
        ["@angular/build"] = "21.2.26",
        ["@angular/cli"] = "21.2.26",
        ["@angular/ssr"] = "21.2.26",
        ["@angular/material"] = "21.2.14",
        ["@angular/cdk"] = "21.2.14",
        ["@ngrx/signals"] = "21.1.1",
        ["angular-oauth2-oidc"] = "21.0.3",
        ["express"] = "5.2.1",
        ["rxjs"] = "7.8.2",
        ["tslib"] = "2.8.1",
        ["@types/express"] = "5.0.6",
        ["@types/node"] = "22.20.5",
        ["jsdom"] = "28.1.0",
        ["typescript"] = "5.9.3",
        ["vitest"] = "4.1.11",
    };
}

/// <summary>Everything the Angular templates may use, derived only from validated plan values and the solution name.</summary>
public sealed record AngularModel(
    string ProjectName,
    string Title,
    bool Material,
    bool SignalStore,
    bool Ssr,
    bool Auth,
    bool Fullstack)
{
    /// <summary>Where the workspace sits in the output: the root, or <c>web/</c> beside a .NET solution.</summary>
    public string Root => Fullstack ? "web/" : "";

    /// <summary>Selects the lockfile, which depends only on the dependency set.</summary>
    public string LockfileKey =>
        $"{(Material ? "material" : "plain")}-{(SignalStore ? "store" : "signals")}-{(Ssr ? "ssr" : "csr")}-{(Auth ? "auth" : "anon")}";

    /// <summary>The API's development URL, the same port the emitted API's launch profile uses.</summary>
    public string ApiDevelopmentUrl { get; init; } = "https://localhost:7000";
}

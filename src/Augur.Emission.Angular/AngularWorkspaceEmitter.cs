using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Augur.Emission.Angular;

/// <summary>Emits the Angular workspace for <c>angular</c> and <c>fullstack</c> plans.</summary>
public sealed class AngularWorkspaceEmitter : IEmitter
{
    /// <summary>Stands in for the project name inside embedded lockfiles.</summary>
    public const string LockfileNamePlaceholder = "__AUGUR_PROJECT_NAME__";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        IndentSize = 2,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly TemplateEngine _templates = new(typeof(AngularWorkspaceEmitter).Assembly);

    public FileSet Emit(EmissionContext context)
    {
        var files = new FileSet();
        if (context.Target is not ("angular" or "fullstack"))
        {
            return files;
        }

        var model = new AngularModel(
            context.AngularProjectName,
            AngularNames.DisplayName(context.SolutionName),
            context.Value("ui-library") == "angular-material",
            context.Value("state-management") == "ngrx-signal-store",
            context.IsTrue("server-side-rendering"),
            context.IsTrue("authentication"),
            context.Target == "fullstack")
        {
            ApiDevelopmentUrl = $"https://localhost:{DevelopmentPorts.Https(context.SolutionName)}",
        };

        var root = model.Root;
        files.Add($"{root}package.json", PackageJson(model));
        files.Add($"{root}angular.json", AngularJson(model));
        if (Lockfile(model) is { } lockfile)
        {
            files.Add($"{root}package-lock.json", lockfile);
        }

        Static(files, model, ".editorconfig", "workspace/editorconfig");
        Static(files, model, ".gitignore", "workspace/gitignore");
        Static(files, model, "tsconfig.json", "workspace/tsconfig.json");
        Render(files, model, "tsconfig.app.json", "workspace/tsconfig.app.json.sbn");
        Static(files, model, "tsconfig.spec.json", "workspace/tsconfig.spec.json");
        Static(files, model, "public/favicon.svg", "workspace/favicon.svg");
        Render(files, model, "src/index.html", "src/index.html.sbn");
        Static(files, model, "src/main.ts", "src/main.ts");
        Render(files, model, "src/styles.css", "src/styles.css.sbn");
        Render(files, model, "src/environments/environment.ts", "src/environment.ts.sbn");

        Render(files, model, "src/app/app.ts", "src/app/app.ts.sbn");
        Render(files, model, "src/app/app.html", model.Material ? "src/app/app.material.html.sbn" : "src/app/app.plain.html.sbn");
        Render(files, model, "src/app/app.config.ts", "src/app/app.config.ts.sbn");
        Render(files, model, "src/app/app.routes.ts", "src/app/app.routes.ts.sbn");
        Render(files, model, "src/app/app.spec.ts", "src/app/app.spec.ts.sbn");
        Static(files, model, "src/app/core/route-focus.ts", "src/app/core/route-focus.ts");
        if (model.SignalStore)
        {
            Static(files, model, "src/app/state/app-store.ts", "src/app/state/app-store.ts");
            Static(files, model, "src/app/state/app-store.spec.ts", "src/app/state/app-store.spec.ts");
        }
        else
        {
            Static(files, model, "src/app/state/app-state.ts", "src/app/state/app-state.ts");
            Static(files, model, "src/app/state/app-state.spec.ts", "src/app/state/app-state.spec.ts");
        }

        Render(files, model, "src/app/pages/home/home.ts", model.Fullstack ? "src/app/pages/home.fullstack.ts.sbn" : "src/app/pages/home.ts.sbn");
        Static(files, model, "src/app/pages/not-found/not-found.ts", "src/app/pages/not-found.ts");
        if (model.Fullstack)
        {
            Static(files, model, "src/app/core/health-api.ts", "src/app/core/health-api.ts");
            Static(files, model, "src/app/core/health-api.spec.ts", "src/app/core/health-api.spec.ts");
            Render(files, model, "proxy.conf.json", "workspace/proxy.conf.json.sbn");
        }

        if (model.Auth)
        {
            Static(files, model, "src/app/auth/auth.service.ts", "src/app/auth/auth.service.ts");
            Static(files, model, "src/app/auth/auth.guard.ts", "src/app/auth/auth.guard.ts");
            Static(files, model, "src/app/auth/auth.guard.spec.ts", "src/app/auth/auth.guard.spec.ts");
            Static(files, model, "src/app/auth/auth.interceptor.ts", "src/app/auth/auth.interceptor.ts");
            Static(files, model, "src/app/auth/auth.interceptor.spec.ts", "src/app/auth/auth.interceptor.spec.ts");
            Static(files, model, "src/app/auth/sign-in.ts", "src/app/auth/sign-in.ts");
            Static(files, model, "src/app/auth/sign-out.ts", "src/app/auth/sign-out.ts");
            Static(files, model, "src/app/pages/notes/notes.ts", "src/app/pages/notes.ts");
        }

        if (model.Ssr)
        {
            Static(files, model, "src/main.server.ts", "src/ssr/main.server.ts");
            Static(files, model, "src/server.ts", "src/ssr/server.ts");
            Static(files, model, "src/app/app.config.server.ts", "src/ssr/app.config.server.ts");
            Render(files, model, "src/app/app.routes.server.ts", "src/ssr/app.routes.server.ts.sbn");
        }

        return files;
    }

    /// <summary><c>package.json</c> with every version exact, so <c>npm ci</c> installs exactly the embedded lockfile.</summary>
    public static string PackageJson(AngularModel model)
    {
        var dependencies = new List<string>
        {
            "@angular/common", "@angular/compiler", "@angular/core", "@angular/forms", "@angular/platform-browser", "@angular/router",
            "rxjs", "tslib",
        };
        var devDependencies = new List<string> { "@angular/build", "@angular/cli", "@angular/compiler-cli", "jsdom", "typescript", "vitest" };
        if (model.Material)
        {
            dependencies.AddRange(["@angular/cdk", "@angular/material"]);
        }

        if (model.SignalStore)
        {
            dependencies.Add("@ngrx/signals");
        }

        if (model.Auth)
        {
            dependencies.Add("angular-oauth2-oidc");
        }

        if (model.Ssr)
        {
            dependencies.AddRange(["@angular/platform-server", "@angular/ssr", "express"]);
            devDependencies.AddRange(["@types/express", "@types/node"]);
        }

        var scripts = new JsonObject
        {
            ["ng"] = "ng",
            ["start"] = "ng serve",
            ["build"] = "ng build",
            ["watch"] = "ng build --watch --configuration development",
            ["test"] = "ng test --watch=false",
        };
        if (model.Ssr)
        {
            scripts[$"serve:ssr:{model.ProjectName}"] = $"node dist/{model.ProjectName}/server/server.mjs";
        }

        var json = new JsonObject
        {
            ["name"] = model.ProjectName,
            ["version"] = "0.0.0",
            ["private"] = true,
            ["scripts"] = scripts,
            ["engines"] = new JsonObject { ["node"] = NpmPackages.NodeEngines },
            ["dependencies"] = Versions(dependencies),
            ["devDependencies"] = Versions(devDependencies),
        };
        return json.ToJsonString(Json) + "\n";
    }

    private static JsonObject Versions(IEnumerable<string> packages) =>
        new(packages.Order(StringComparer.Ordinal).Select(p => KeyValuePair.Create(p, (JsonNode?)NpmPackages.Versions[p])));

    private static string AngularJson(AngularModel model)
    {
        var name = model.ProjectName;
        var styles = new JsonArray();
        if (model.Material)
        {
            styles.Add("@angular/material/prebuilt-themes/azure-blue.css");
        }

        styles.Add("src/styles.css");

        var buildOptions = new JsonObject
        {
            ["browser"] = "src/main.ts",
            ["tsConfig"] = "tsconfig.app.json",
            ["assets"] = new JsonArray(new JsonObject { ["glob"] = "**/*", ["input"] = "public" }),
            ["styles"] = styles,
        };
        if (model.Ssr)
        {
            buildOptions["server"] = "src/main.server.ts";
            buildOptions["outputMode"] = "server";
            buildOptions["security"] = new JsonObject { ["allowedHosts"] = new JsonArray() };
            buildOptions["ssr"] = new JsonObject { ["entry"] = "src/server.ts" };
        }

        var serveOptions = new JsonObject();
        if (model.Fullstack)
        {
            serveOptions["proxyConfig"] = "proxy.conf.json";
        }

        var serve = new JsonObject
        {
            ["builder"] = "@angular/build:dev-server",
            ["configurations"] = new JsonObject
            {
                ["production"] = new JsonObject { ["buildTarget"] = $"{name}:build:production" },
                ["development"] = new JsonObject { ["buildTarget"] = $"{name}:build:development" },
            },
            ["defaultConfiguration"] = "development",
        };
        if (serveOptions.Count > 0)
        {
            serve["options"] = serveOptions;
        }

        var json = new JsonObject
        {
            ["$schema"] = "./node_modules/@angular/cli/lib/config/schema.json",
            ["version"] = 1,
            ["cli"] = new JsonObject { ["packageManager"] = "npm", ["analytics"] = false },
            ["newProjectRoot"] = "projects",
            ["projects"] = new JsonObject
            {
                [name] = new JsonObject
                {
                    ["projectType"] = "application",
                    ["schematics"] = new JsonObject(),
                    ["root"] = "",
                    ["sourceRoot"] = "src",
                    ["prefix"] = "app",
                    ["architect"] = new JsonObject
                    {
                        ["build"] = new JsonObject
                        {
                            ["builder"] = "@angular/build:application",
                            ["options"] = buildOptions,
                            ["configurations"] = new JsonObject
                            {
                                ["production"] = new JsonObject
                                {
                                    ["budgets"] = new JsonArray(
                                        new JsonObject { ["type"] = "initial", ["maximumWarning"] = "500kB", ["maximumError"] = "1MB" },
                                        new JsonObject { ["type"] = "anyComponentStyle", ["maximumWarning"] = "4kB", ["maximumError"] = "8kB" }),
                                    ["outputHashing"] = "all",
                                },
                                ["development"] = new JsonObject
                                {
                                    ["optimization"] = false,
                                    ["extractLicenses"] = false,
                                    ["sourceMap"] = true,
                                },
                            },
                            ["defaultConfiguration"] = "production",
                        },
                        ["serve"] = serve,
                        ["test"] = new JsonObject { ["builder"] = "@angular/build:unit-test" },
                    },
                },
            },
        };
        return json.ToJsonString(Json) + "\n";
    }

    /// <summary>The embedded lockfile for this dependency set, with the project name filled in.</summary>
    private string? Lockfile(AngularModel model)
    {
        using var stream = typeof(AngularWorkspaceEmitter).Assembly.GetManifestResourceStream($"lockfiles/{model.LockfileKey}.json.gz");
        if (stream is null)
        {
#if AUGUR_BOOTSTRAP_LOCKFILES
            return null;
#else
            throw new InvalidOperationException(
                $"augur has no package-lock.json for the {model.LockfileKey} workspace; run eng/Regenerate-AngularLockfiles.ps1");
#endif
        }

        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        return reader.ReadToEnd().Replace(LockfileNamePlaceholder, model.ProjectName, StringComparison.Ordinal);
    }

    private void Static(FileSet files, AngularModel model, string path, string template) =>
        files.Add(model.Root + path, Encoding.UTF8.GetString(_templates.Static(template)));

    private void Render(FileSet files, AngularModel model, string path, string template) =>
        files.Add(model.Root + path, _templates.Render(template, model));
}

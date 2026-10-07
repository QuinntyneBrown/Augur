using System.Text;
using System.Text.RegularExpressions;
using Augur.Tests.Support;

namespace Augur.Tests.Acceptance;

public sealed class DotNetEmissionTests
{
    [Fact]
    public async Task A_clean_architecture_plan_emits_exactly_its_layers_and_no_worker()
    {
        var files = await Emit(Plans.DotNet("clean-architecture", "ef-core-postgresql", authentication: true, worker: false, cqrs: true));

        var projects = files.Keys.Where(p => p.EndsWith(".csproj", StringComparison.Ordinal)).Select(Path.GetFileNameWithoutExtension);
        Assert.Equal(
            ["Contoso.Orders.Api", "Contoso.Orders.Application", "Contoso.Orders.Domain", "Contoso.Orders.Infrastructure", "Contoso.Orders.Tests"],
            projects.Order(StringComparer.Ordinal));
        Assert.Contains("Contoso.Orders.slnx", files.Keys);
        Assert.Contains("Directory.Build.props", files.Keys);
        Assert.Contains(".editorconfig", files.Keys);
        Assert.Contains(".gitignore", files.Keys);
        Assert.Contains("Contoso.Orders.Application/Notes/Commands/CreateNote.cs", files.Keys);
        Assert.Contains("Contoso.Orders.Application/Notes/Queries/ListNotes.cs", files.Keys);
        Assert.Contains("Npgsql.EntityFrameworkCore.PostgreSQL", files["Contoso.Orders.Infrastructure/Contoso.Orders.Infrastructure.csproj"]);
        Assert.Contains("<Nullable>enable</Nullable>", files["Directory.Build.props"]);
        Assert.Contains("<TreatWarningsAsErrors>true</TreatWarningsAsErrors>", files["Directory.Build.props"]);
    }

    [Fact]
    public async Task A_vertical_slice_plan_emits_one_api_project_with_a_features_folder()
    {
        var files = await Emit(Plans.DotNet("vertical-slice", "ef-core-sqlite", authentication: false, worker: false, cqrs: false));

        Assert.Equal(["Contoso.Orders.Api", "Contoso.Orders.Tests"], Projects(files));
        Assert.Contains(files.Keys, p => p.StartsWith("Contoso.Orders.Api/Features/Notes/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_minimal_api_plan_maps_endpoints_without_controllers()
    {
        var files = await Emit(Plans.DotNet("minimal-api", "none", authentication: false, worker: false, cqrs: null));

        Assert.Equal(["Contoso.Orders.Api", "Contoso.Orders.Tests"], Projects(files));
        Assert.Contains("MapGroup(\"/api/notes\")", files["Contoso.Orders.Api/Notes/NotesEndpoints.cs"]);
        Assert.DoesNotContain(files.Values, content => content.Contains("Controller", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Background_processing_adds_a_worker_project_built_on_background_service()
    {
        var files = await Emit(Plans.DotNet("minimal-api", "none", authentication: false, worker: true, cqrs: null));

        Assert.Contains("Contoso.Orders.Worker/Contoso.Orders.Worker.csproj", files.Keys);
        Assert.Contains(": BackgroundService", files["Contoso.Orders.Worker/ScheduledWorker.cs"]);
    }

    [Fact]
    public async Task Every_dotnet_combination_meets_the_structure_and_security_rules()
    {
        var plans = Plans.AllDotNet().ToList();
        Assert.Equal(80, plans.Count);
        foreach (var decisions in plans)
        {
            var files = await Emit(decisions);
            var label = Plans.Describe(decisions);
            var all = string.Join('\n', files.Values);

            Assert.True(Regex.Matches(all, @"<PackageReference [^>]*>").All(r => Regex.IsMatch(r.Value, @"Version=""\d+\.\d+\.\d+""")), $"{label}: a package version is not exact");
            Assert.DoesNotContain("AllowAnyOrigin", all);
            Assert.DoesNotMatch(@"(?i)-----BEGIN [A-Z ]*PRIVATE KEY-----|password\s*=|pwd\s*=|signingkey|issuersigningkey\s*=\s*new", all);
            if ((string)decisions["persistence"] == "none")
            {
                Assert.False(all.Contains("Microsoft.EntityFrameworkCore", StringComparison.Ordinal), $"{label}: references EF Core without persistence");
            }

            if (!(bool)decisions["authentication"])
            {
                Assert.False(Regex.IsMatch(all, @"AddAuthentication|UseAuthentication|AddJwtBearer"), $"{label}: registers authentication");
            }

            if ((bool)decisions["background-processing"] is false)
            {
                Assert.DoesNotContain(files.Keys, p => p.Contains(".Worker/", StringComparison.Ordinal));
            }

            Assert.All(files, file =>
            {
                Assert.DoesNotContain('\r', file.Value);
                Assert.False(file.Value.StartsWith('﻿'), $"{label}: {file.Key} has a BOM");
            });
        }
    }

    [Fact]
    public async Task An_angular_plan_emits_no_dotnet_solution()
    {
        var files = await Emit(Plans.Angular("angular-material", "signals", ssr: false, authentication: false));

        Assert.DoesNotContain(files.Keys, p => p.EndsWith(".slnx", StringComparison.Ordinal) || p.EndsWith(".sln", StringComparison.Ordinal) || p.EndsWith(".csproj", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_hand_edited_plan_switching_to_postgresql_emits_postgresql()
    {
        using var cli = new CliRunner();
        cli.WriteFile("spec.md", "Build an order tracking API.\n");
        var script = AnswerScript.DotNetMinimalApi().WriteTo(cli);
        await cli.RunAsync("plan", "--spec", "spec.md", "--name", "Contoso.Orders", "--oracle-script", script, "--out", "plan.json");
        cli.WriteFile("plan.json", cli.ReadFile("plan.json").Replace("\"ef-core-sqlite\"", "\"ef-core-postgresql\"", StringComparison.Ordinal));

        var result = await cli.RunAsync("emit", "--plan", "plan.json", "--out", "out");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Npgsql.EntityFrameworkCore.PostgreSQL", cli.ReadFile("out/Contoso.Orders.Api/Contoso.Orders.Api.csproj"));
        Assert.Contains("UseNpgsql", cli.ReadFile("out/Contoso.Orders.Api/Notes/NotesServices.cs"));
    }

    [Theory]
    [InlineData("clean-postgresql-auth-cqrs")]
    [InlineData("vertical-sqlite-worker")]
    [InlineData("minimal-none")]
    public async Task Emitted_solutions_match_their_golden_copies(string name)
    {
        var decisions = name switch
        {
            "clean-postgresql-auth-cqrs" => Plans.DotNet("clean-architecture", "ef-core-postgresql", authentication: true, worker: false, cqrs: true),
            "vertical-sqlite-worker" => Plans.DotNet("vertical-slice", "ef-core-sqlite", authentication: false, worker: true, cqrs: true),
            _ => Plans.DotNet("minimal-api", "none", authentication: false, worker: false, cqrs: null),
        };

        Golden.Compare($"dotnet/{name}", await EmitBytes(decisions));
    }

    internal static async Task<SortedDictionary<string, string>> Emit(IReadOnlyDictionary<string, object> decisions) =>
        new((await EmitBytes(decisions)).ToDictionary(f => f.Key, f => Encoding.UTF8.GetString(f.Value)), StringComparer.Ordinal);

    internal static async Task<SortedDictionary<string, byte[]>> EmitBytes(IReadOnlyDictionary<string, object> decisions, string name = "Contoso.Orders")
    {
        using var cli = new CliRunner();
        cli.WriteFile("plan.json", Plans.Json(name, decisions));
        var result = await cli.RunAsync("emit", "--plan", "plan.json", "--out", "out");
        Assert.True(result.ExitCode == 0, result.Stderr);
        return EmissionTests.Snapshot(cli.PathOf("out"));
    }

    private static string[] Projects(IReadOnlyDictionary<string, string> files) =>
        [.. files.Keys.Where(p => p.EndsWith(".csproj", StringComparison.Ordinal)).Select(p => Path.GetFileNameWithoutExtension(p)!).Order(StringComparer.Ordinal)];
}

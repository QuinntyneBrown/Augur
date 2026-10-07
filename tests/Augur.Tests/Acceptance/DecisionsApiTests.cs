using System.Text;
using System.Text.Json.Nodes;
using Augur.Tests.Support;

namespace Augur.Tests.Acceptance;

public sealed class DecisionsApiTests
{
    private const string Key = "sk-test-KEY456";
    private const string Spec = "Build an order tracking app with sign-in for staff.\n";

    [Fact]
    public async Task Each_request_posts_one_question_per_pending_decision_with_the_key_and_default_model()
    {
        await using var stub = await StubDecisionsServer.StartAsync(AnswerScript.DotNetMinimalApi());
        using var cli = NewCli(stub);

        var result = await Plan(cli);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(2, stub.Requests.Count);
        foreach (var request in stub.Requests)
        {
            Assert.Equal("POST", request.Method);
            Assert.Equal("/v1/decisions", request.Path);
            Assert.StartsWith("application/json", request.Headers["Content-Type"]);
            Assert.Equal($"Bearer {Key}", request.Headers["Authorization"]);
            Assert.Equal("gpt-6-luna", request.Body["model"]!.GetValue<string>());
        }

        Assert.Equal(["target", "authentication"], stub.Requests[0].QuestionNames);
        Assert.Equal(["architecture", "persistence", "background-processing"], stub.Requests[1].QuestionNames);
    }

    [Fact]
    public async Task The_model_option_sets_the_request_model()
    {
        await using var stub = await StubDecisionsServer.StartAsync(AnswerScript.DotNetMinimalApi());
        using var cli = NewCli(stub);

        await Plan(cli, "--model", "gpt-6-luna-2026-09");

        Assert.All(stub.Requests, r => Assert.Equal("gpt-6-luna-2026-09", r.Body["model"]!.GetValue<string>()));
    }

    [Fact]
    public async Task Choice_options_and_score_levels_are_sent_with_their_catalog_descriptions()
    {
        await using var stub = await StubDecisionsServer.StartAsync(AnswerScript.FullstackCleanArchitecture());
        using var cli = NewCli(stub);

        await Plan(cli);

        var questions = stub.Requests.SelectMany(r => r.Body["questions"]!.AsArray()).ToList();
        var target = questions.Single(q => q!["name"]!.GetValue<string>() == "target")!;
        Assert.Equal("choice", target["type"]!.GetValue<string>());
        Assert.Equal(["fullstack", "dotnet", "angular", "other"], target["choices"]!.AsArray().Select(c => c!["value"]!.GetValue<string>()));
        Assert.All(target["choices"]!.AsArray(), c => Assert.False(string.IsNullOrWhiteSpace(c!["description"]!.GetValue<string>())));
        Assert.StartsWith("Decide what kind of software", target["instructions"]!.GetValue<string>());

        var complexity = questions.Single(q => q!["name"]!.GetValue<string>() == "domain-complexity")!;
        Assert.Equal("score", complexity["type"]!.GetValue<string>());
        Assert.Equal(["trivial", "crud", "business-rules", "complex-domain"], complexity["levels"]!.AsArray().Select(l => l!["label"]!.GetValue<string>()));

        var authentication = questions.Single(q => q!["name"]!.GetValue<string>() == "authentication")!;
        Assert.Equal("predicate", authentication["type"]!.GetValue<string>());
        Assert.Null(authentication["choices"]);
    }

    [Fact]
    public async Task The_specification_and_images_are_sent_as_one_user_message_in_command_line_order()
    {
        await using var stub = await StubDecisionsServer.StartAsync(AnswerScript.DotNetMinimalApi());
        using var cli = NewCli(stub);
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 4, 5];
        cli.WriteBytes("sketch.jpg", png);
        cli.WriteBytes("photo.bin", jpeg);

        var result = await Plan(cli, "--image", "sketch.jpg", "--image", "photo.bin");

        Assert.Equal(0, result.ExitCode);
        var input = stub.Requests[0].Body["input"]!.AsArray();
        var message = Assert.Single(input)!;
        Assert.Equal("user", message["role"]!.GetValue<string>());
        var content = message["content"]!.AsArray();
        Assert.Equal(3, content.Count);
        Assert.Equal("input_text", content[0]!["type"]!.GetValue<string>());
        Assert.Equal(Spec, content[0]!["text"]!.GetValue<string>());
        Assert.Equal("input_image", content[1]!["type"]!.GetValue<string>());
        Assert.Equal($"data:image/png;base64,{Convert.ToBase64String(png)}", content[1]!["image_url"]!.GetValue<string>());
        Assert.Equal($"data:image/jpeg;base64,{Convert.ToBase64String(jpeg)}", content[2]!["image_url"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("fullstack", "clean-architecture", 3)]
    [InlineData("fullstack", "minimal-api", 2)]
    [InlineData("angular", null, 2)]
    public async Task One_request_is_made_per_dependency_level_with_pending_decisions(string target, string? architecture, int requests)
    {
        var answers = AnswerScript.FullstackCleanArchitecture().Choice("target", target);
        if (architecture is not null)
        {
            answers.Choice("architecture", architecture);
        }

        await using var stub = await StubDecisionsServer.StartAsync(answers);
        using var cli = NewCli(stub);

        var result = await Plan(cli);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(requests, stub.Requests.Count);
        Assert.Matches($@"api requests {requests},", result.Stderr);
    }

    [Fact]
    public async Task Overrides_are_never_asked_and_dependent_levels_are_batched()
    {
        await using var stub = await StubDecisionsServer.StartAsync(AnswerScript.FullstackCleanArchitecture());
        using var cli = NewCli(stub);

        var result = await Plan(cli, "--set", "target=fullstack", "--set", "architecture=minimal-api");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(2, stub.Requests.Count);
        Assert.Equal(["authentication"], stub.Requests[0].QuestionNames);
        Assert.Equal(
            ["persistence", "background-processing", "ui-library", "state-management", "server-side-rendering"],
            stub.Requests[1].QuestionNames);
        var architecture = JsonNode.Parse(result.Stdout)!["decisions"]!["architecture"]!;
        Assert.Equal("minimal-api", architecture["value"]!.GetValue<string>());
        Assert.Equal("override", architecture["source"]!.GetValue<string>());
        Assert.Null(JsonNode.Parse(result.Stdout)!["decisions"]!["domain-complexity"]);
    }

    [Fact]
    public async Task Api_answers_are_recorded_with_source_api_and_the_model()
    {
        await using var stub = await StubDecisionsServer.StartAsync(AnswerScript.FullstackCleanArchitecture());
        using var cli = NewCli(stub);

        var result = await Plan(cli);

        Assert.Equal(0, result.ExitCode);
        var entries = JsonNode.Parse(cli.ReadFile("decisions.json"))!["entries"]!.AsArray();
        Assert.Equal(9, entries.Count);
        Assert.All(entries, e =>
        {
            Assert.Equal("api", e!["source"]!.GetValue<string>());
            Assert.Equal("gpt-6-luna", e["model"]!.GetValue<string>());
        });
    }

    [Fact]
    public async Task A_rerun_with_a_lockfile_makes_no_request_and_writes_the_same_plan()
    {
        await using var stub = await StubDecisionsServer.StartAsync(AnswerScript.FullstackCleanArchitecture());
        using var cli = NewCli(stub);
        var first = await Plan(cli);

        var second = await Plan(cli);

        Assert.Equal(0, second.ExitCode);
        Assert.Equal(3, stub.Requests.Count);
        Assert.Equal(first.Stdout, second.Stdout);
        Assert.Matches(@"resolved 9 \(api 0, lockfile 9, override 0, fallback 0, user 0, script 0\), skipped 0, api requests 0, elapsed", second.Stderr);
    }

    [Fact]
    public async Task Changing_the_specification_asks_everything_again()
    {
        await using var stub = await StubDecisionsServer.StartAsync(AnswerScript.FullstackCleanArchitecture());
        using var cli = NewCli(stub);
        await Plan(cli);
        cli.WriteFile("spec.md", Spec + "!");

        await Plan(cli);

        Assert.Equal(6, stub.Requests.Count);
    }

    [Theory]
    [InlineData("architecture", "choice", "serverless", "'serverless' is not one of clean-architecture, vertical-slice, minimal-api, other")]
    [InlineData("authentication", "predicate", "1.3", "probability 1.3 is outside 0 to 1")]
    [InlineData("domain-complexity", "score", "3.2", "score 3.2 is outside 0 to 3")]
    public async Task An_answer_outside_the_closed_set_is_reported_and_exits_with_4(string id, string type, string value, string problem)
    {
        var answers = AnswerScript.FullstackCleanArchitecture();
        _ = type switch
        {
            "choice" => answers.Choice(id, value),
            "predicate" => answers.Predicate(id, double.Parse(value, System.Globalization.CultureInfo.InvariantCulture)),
            _ => answers.Score(id, double.Parse(value, System.Globalization.CultureInfo.InvariantCulture), 0.9),
        };
        await using var stub = await StubDecisionsServer.StartAsync(answers);
        using var cli = NewCli(stub);

        var result = await Plan(cli);

        Assert.Equal(4, result.ExitCode);
        Assert.EndsWith($"\nerror: invalid answer for {id} from the Decisions API: {problem}\n", result.Stderr);
    }

    [Fact]
    public async Task A_response_without_an_answer_for_a_question_is_an_api_failure()
    {
        await using var stub = await StubDecisionsServer.StartAsync((request, _) => Task.FromResult(new StubResponse(200, new JsonObject { ["answers"] = new JsonArray() }, "req_empty")));
        using var cli = NewCli(stub);

        var result = await Plan(cli);

        Assert.Equal(4, result.ExitCode);
        Assert.EndsWith("error: the Decisions API response has no answer for target (request id req_empty)\n", result.Stderr);
    }

    [Fact]
    public async Task A_missing_api_key_is_reported_before_any_request()
    {
        await using var stub = await StubDecisionsServer.StartAsync(AnswerScript.DotNetMinimalApi());
        using var cli = NewCli(stub).WithEnv("OPENAI_API_KEY", null);

        var result = await Plan(cli);

        Assert.Equal(2, result.ExitCode);
        Assert.Empty(stub.Requests);
        Assert.EndsWith("error: OPENAI_API_KEY is not set; it is needed to ask the Decisions API (or use --set, --oracle-script, or --offline)\n", result.Stderr);
    }

    [Fact]
    public async Task A_plain_http_base_url_is_rejected_for_a_non_loopback_host()
    {
        using var cli = new CliRunner().WithEnv("OPENAI_API_KEY", Key).WithEnv("AUGUR_OPENAI_BASE_URL", "http://example.com");
        cli.WriteFile("spec.md", Spec);

        var result = await Plan(cli);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("error: AUGUR_OPENAI_BASE_URL must use https; only HTTPS is allowed for non-loopback hosts\n", result.Stderr);
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    public async Task A_plain_http_base_url_is_allowed_for_loopback_hosts(string host)
    {
        await using var stub = await StubDecisionsServer.StartAsync(AnswerScript.DotNetMinimalApi());
        using var cli = NewCli(stub).WithEnv("AUGUR_OPENAI_BASE_URL", stub.BaseUrl.Replace("127.0.0.1", host, StringComparison.Ordinal));

        var result = await Plan(cli);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(2, stub.Requests.Count);
    }

    [Fact]
    public async Task A_fully_replayed_or_overridden_run_needs_no_valid_base_url_or_key()
    {
        using var cli = new CliRunner().WithEnv("AUGUR_OPENAI_BASE_URL", "http://example.com");
        cli.WriteFile("spec.md", Spec);

        var result = await cli.RunAsync(
            "plan", "--spec", "spec.md", "--name", "Contoso.Orders",
            "--set", "target=dotnet", "--set", "authentication=false", "--set", "architecture=minimal-api",
            "--set", "persistence=none", "--set", "background-processing=false");

        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task The_disclosure_notice_names_the_host_and_files_before_the_first_request()
    {
        await using var stub = await StubDecisionsServer.StartAsync(AnswerScript.DotNetMinimalApi());
        using var cli = NewCli(stub);
        cli.WriteBytes("ui.png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 9, 9]);

        var result = await Plan(cli, "--image", "ui.png");

        Assert.Equal(0, result.ExitCode);
        Assert.StartsWith(
            $"sending to the Decisions API at 127.0.0.1: spec.md ({Encoding.UTF8.GetByteCount(Spec)} bytes), ui.png (10 bytes)\n",
            result.Stderr);
    }

    [Fact]
    public async Task A_fully_replayed_run_writes_no_disclosure_notice()
    {
        await using var stub = await StubDecisionsServer.StartAsync(AnswerScript.DotNetMinimalApi());
        using var cli = NewCli(stub);
        await Plan(cli);

        var result = await Plan(cli);

        Assert.DoesNotContain("sending to the Decisions API", result.Stderr);
    }

    [Fact]
    public async Task Diagnostic_verbosity_logs_each_request_without_the_specification()
    {
        await using var stub = await StubDecisionsServer.StartAsync(AnswerScript.DotNetMinimalApi());
        using var cli = NewCli(stub);

        var result = await Plan(cli, "--verbosity", "diagnostic");

        Assert.Equal(0, result.ExitCode);
        Assert.Matches(@"(?m)^http: POST /v1/decisions -> 200 in \d+ ms \(attempt 1, request id req_1\)$", result.Stderr);
        for (var start = 0; start + 32 <= Spec.Length; start++)
        {
            Assert.DoesNotContain(Spec.Substring(start, 32), result.Stderr);
        }
    }

    private static CliRunner NewCli(StubDecisionsServer stub)
    {
        var cli = new CliRunner().WithEnv("OPENAI_API_KEY", Key).WithEnv("AUGUR_OPENAI_BASE_URL", stub.BaseUrl);
        cli.WriteFile("spec.md", Spec);
        return cli;
    }

    private static Task<CliResult> Plan(CliRunner cli, params string[] extra) =>
        cli.RunAsync(["plan", "--spec", "spec.md", "--name", "Contoso.Orders", .. extra]);
}

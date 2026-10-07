using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Augur.Tests.Support;

/// <summary>A loopback stand-in for the Decisions API that records every request and answers from a script.</summary>
public sealed class StubDecisionsServer : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly ConcurrentQueue<StubRequest> _requests = new();
    private int _count;

    private StubDecisionsServer(WebApplication app, Func<StubRequest, int, Task<StubResponse>> handler)
    {
        _app = app;
        Handler = handler;
    }

    /// <summary>Decides the response to the n-th request (1-based).</summary>
    public Func<StubRequest, int, Task<StubResponse>> Handler { get; set; }

    public string BaseUrl { get; private set; } = "";

    public IReadOnlyList<StubRequest> Requests => [.. _requests];

    public static Task<StubDecisionsServer> StartAsync(AnswerScript answers) =>
        StartAsync((request, _) => Task.FromResult(StubResponse.Answer(answers, request)));

    public static async Task<StubDecisionsServer> StartAsync(Func<StubRequest, int, Task<StubResponse>> handler, bool untrustedHttps = false)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0, listen =>
        {
            if (untrustedHttps)
            {
                listen.UseHttps(SelfSignedCertificate());
            }
        }));
        var app = builder.Build();
        var server = new StubDecisionsServer(app, handler);
        app.MapPost("/v1/decisions", server.HandleAsync);
        await app.StartAsync();
        var address = app.Urls.Single();
        server.BaseUrl = address;
        return server;
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    private async Task HandleAsync(HttpContext context)
    {
        var body = await JsonNode.ParseAsync(context.Request.Body);
        var request = new StubRequest(
            DateTimeOffset.UtcNow,
            context.Request.Method,
            context.Request.Path,
            context.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase),
            body!);
        _requests.Enqueue(request);
        var response = await Handler(request, Interlocked.Increment(ref _count));
        if (response.NeverRespond)
        {
            await Task.Delay(Timeout.Infinite, context.RequestAborted).ContinueWith(_ => { }, TaskScheduler.Default);
            return;
        }

        context.Response.StatusCode = response.Status;
        context.Response.Headers["x-request-id"] = response.RequestId ?? $"req_{_count}";
        if (response.RetryAfter is { } retryAfter)
        {
            context.Response.Headers.RetryAfter = retryAfter;
        }

        if (response.Body is { } json)
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(json.ToJsonString());
        }
    }

    private static X509Certificate2 SelfSignedCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        san.AddIpAddress(IPAddress.Loopback);
        san.AddDnsName("localhost");
        request.CertificateExtensions.Add(san.Build());
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx), password: null);
    }
}

public sealed record StubRequest(DateTimeOffset At, string Method, string Path, IReadOnlyDictionary<string, string> Headers, JsonNode Body)
{
    public IReadOnlyList<string> QuestionNames => [.. Body["questions"]!.AsArray().Select(q => q!["name"]!.GetValue<string>())];
}

public sealed record StubResponse(int Status, JsonNode? Body = null, string? RequestId = null, string? RetryAfter = null, bool NeverRespond = false)
{
    public static StubResponse Never { get; } = new(0, NeverRespond: true);

    public static StubResponse Error(int status, string? retryAfter = null) =>
        new(status, new JsonObject { ["error"] = new JsonObject { ["message"] = "stub error" } }, RetryAfter: retryAfter);

    /// <summary>Answers each question in the request from <paramref name="script"/>, in Decisions API format.</summary>
    public static StubResponse Answer(AnswerScript script, StubRequest request) => new(200, new JsonObject
    {
        ["answers"] = new JsonArray([.. request.Body["questions"]!.AsArray().Select(question =>
        {
            var name = question!["name"]!.GetValue<string>();
            var answer = script.AnswerFor(name) ?? throw new InvalidOperationException($"the stub has no answer for {name}");
            answer["name"] = name;
            answer["type"] ??= question["type"]!.GetValue<string>();
            return answer;
        })]),
    });
}

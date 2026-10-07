using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Authentication;
using Augur.Core;
using Augur.Core.Decisions;
using Augur.Core.Intake;

namespace Augur.Oracle.OpenAI;

/// <summary>How to reach the Decisions API, from the environment and the command line.</summary>
public sealed record DecisionsApiSettings(string? BaseUrl, string? ApiKey, string Model, TimeSpan AttemptTimeout)
{
    public const string ApiKeyVariable = "OPENAI_API_KEY";
    public const string BaseUrlVariable = "AUGUR_OPENAI_BASE_URL";
    public const string DefaultBaseUrl = "https://api.openai.com";
}

/// <summary>
/// Asks the OpenAI Decisions API. The base URL and key are checked, and the data disclosure notice written,
/// only when the first question actually has to be sent.
/// </summary>
public sealed class DecisionsApiOracle(DecisionsApiSettings settings, IReporter reporter) : IDecisionOracle, IApiRequestCounter, IDisposable
{
    public const int MaxRetries = 3;

    private static readonly HashSet<HttpStatusCode> Transient =
    [
        HttpStatusCode.RequestTimeout,
        HttpStatusCode.TooManyRequests,
        HttpStatusCode.InternalServerError,
        HttpStatusCode.BadGateway,
        HttpStatusCode.ServiceUnavailable,
        HttpStatusCode.GatewayTimeout,
    ];

    private HttpClient? _client;
    private Uri? _endpoint;
    private string? _apiKey;
    private (SpecificationInput Input, byte[] Json)? _encodedInput;

    public int ApiRequests { get; private set; }

    public async Task<IReadOnlyList<DecisionAnswer>> AnswerAsync(
        SpecificationInput input,
        IReadOnlyList<DecisionRequest> requests,
        CancellationToken cancellationToken)
    {
        if (_client is null)
        {
            Connect(input);
        }

        if (_encodedInput?.Input != input)
        {
            _encodedInput = (input, DecisionsRequestBuilder.Input(input));
        }

        var body = DecisionsRequestBuilder.Build(_encodedInput.Value.Json, requests, settings.Model);
        ApiRequests++;
        var (responseBody, requestId) = await SendWithRetriesAsync(body, cancellationToken);
        return DecisionsResponseParser.Parse(responseBody, requests, settings.Model, requestId);
    }

    public void Dispose() => _client?.Dispose();

    private void Connect(SpecificationInput input)
    {
        var baseUrl = BaseUrlPolicy.Resolve(settings.BaseUrl);
        _apiKey = string.IsNullOrEmpty(settings.ApiKey)
            ? throw new UsageException(
                $"{DecisionsApiSettings.ApiKeyVariable} is not set; it is needed to ask the Decisions API (or use --set, --oracle-script, or --offline)")
            : settings.ApiKey;
        _endpoint = new Uri(baseUrl.AbsoluteUri.TrimEnd('/') + "/v1/decisions");

        var files = new List<string> { $"{input.SpecDisplayName} ({input.TextByteCount} bytes)" };
        files.AddRange(input.Images.Select(i => $"{i.DisplayName} ({i.Bytes.Length} bytes)"));
        reporter.Info($"sending to the Decisions API at {baseUrl.Host}: {string.Join(", ", files)}");

        _client = new HttpClient(new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All })
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }

    private async Task<(string Body, string? RequestId)> SendWithRetriesAsync(IReadOnlyList<ReadOnlyMemory<byte>> body, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var outcome = await SendOnceAsync(body, attempt, cancellationToken);
            if (outcome.Success is { } success)
            {
                return success;
            }

            if (!outcome.Retryable || attempt > MaxRetries)
            {
                throw outcome.Failure(attempt);
            }

            await Task.Delay(Backoff(attempt, outcome.RetryAfter), cancellationToken);
        }
    }

    private async Task<AttemptOutcome> SendOnceAsync(IReadOnlyList<ReadOnlyMemory<byte>> body, int attempt, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(settings.AttemptTimeout);
        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new SegmentsContent(body) { Headers = { ContentType = new MediaTypeHeaderValue("application/json") } },
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        var clock = Stopwatch.StartNew();
        try
        {
            using var response = await _client!.SendAsync(request, HttpCompletionOption.ResponseContentRead, timeout.Token);
            var requestId = response.Headers.TryGetValues("x-request-id", out var ids) ? ids.FirstOrDefault() : null;
            Log(attempt, clock, $"{(int)response.StatusCode}", requestId);
            if (response.IsSuccessStatusCode)
            {
                return AttemptOutcome.Succeeded(await response.Content.ReadAsStringAsync(timeout.Token), requestId);
            }

            return AttemptOutcome.Failed(Transient.Contains(response.StatusCode), RetryAfter(response), attempts =>
                response.StatusCode == HttpStatusCode.Unauthorized
                    ? new DecisionsApiException($"the Decisions API rejected the API key (HTTP 401, request id {requestId ?? "unknown"})")
                    : new DecisionsApiException(
                        $"the Decisions API returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}"
                        + (attempts > 1 ? $" after {attempts} attempts" : "")
                        + $" (request id {requestId ?? "unknown"})"));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Log(attempt, clock, "timed out", null);
            var seconds = settings.AttemptTimeout.TotalSeconds.ToString(CultureInfo.InvariantCulture);
            return AttemptOutcome.Failed(true, null, attempts =>
                new DecisionsApiException($"the Decisions API did not respond within {seconds} s after {attempts} attempts"));
        }
        catch (HttpRequestException ex) when (ex.InnerException is AuthenticationException tls)
        {
            Log(attempt, clock, "TLS failure", null);
            return AttemptOutcome.Failed(false, null, _ =>
                new DecisionsApiException($"could not make a trusted HTTPS connection to {_endpoint!.Authority}: {tls.Message}", ex));
        }
        catch (HttpRequestException ex)
        {
            Log(attempt, clock, "network error", null);
            return AttemptOutcome.Failed(true, null, attempts =>
                new DecisionsApiException($"could not reach the Decisions API at {_endpoint!.Authority} after {attempts} attempts: {ex.Message}", ex));
        }
    }

    /// <summary>Exponential backoff from 1 s with up to 25 % jitter, capped at 20 s; at least as long as Retry-After.</summary>
    private static TimeSpan Backoff(int attempt, TimeSpan? retryAfter)
    {
        var baseSeconds = Math.Min(20, Math.Pow(2, attempt - 1));
        var jittered = TimeSpan.FromSeconds(Math.Min(20, baseSeconds * (1 + (Random.Shared.NextDouble() * 0.25))));
        return retryAfter is { } wait && wait > jittered ? wait : jittered;
    }

    private static TimeSpan? RetryAfter(HttpResponseMessage response) => response.Headers.RetryAfter switch
    {
        { Delta: { } delta } => delta,
        { Date: { } date } => date - DateTimeOffset.UtcNow is var wait && wait > TimeSpan.Zero ? wait : TimeSpan.Zero,
        _ => null,
    };

    private void Log(int attempt, Stopwatch clock, string outcome, string? requestId) =>
        reporter.Diagnostic(
            $"http: POST {_endpoint!.AbsolutePath} -> {outcome} in {clock.ElapsedMilliseconds} ms (attempt {attempt}"
            + (requestId is null ? ")" : $", request id {requestId})"));

    private sealed record AttemptOutcome(
        (string Body, string? RequestId)? Success,
        bool Retryable,
        TimeSpan? RetryAfter,
        Func<int, Exception> Failure)
    {
        public static AttemptOutcome Succeeded(string body, string? requestId) =>
            new((body, requestId), false, null, _ => new InvalidOperationException());

        public static AttemptOutcome Failed(bool retryable, TimeSpan? retryAfter, Func<int, Exception> failure) =>
            new(null, retryable, retryAfter, failure);
    }
}

/// <summary>Decides where requests go: HTTPS only, except plain HTTP to a loopback test server.</summary>
public static class BaseUrlPolicy
{
    /// <exception cref="UsageException">The URL is malformed or uses plain HTTP for a non-loopback host.</exception>
    public static Uri Resolve(string? configured)
    {
        var value = string.IsNullOrWhiteSpace(configured) ? DecisionsApiSettings.DefaultBaseUrl : configured;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new UsageException($"{DecisionsApiSettings.BaseUrlVariable} is not a valid http or https URL");
        }

        if (uri.Scheme != Uri.UriSchemeHttps && uri.DnsSafeHost is not ("localhost" or "127.0.0.1" or "::1"))
        {
            throw new UsageException($"{DecisionsApiSettings.BaseUrlVariable} must use https; only HTTPS is allowed for non-loopback hosts");
        }

        return uri;
    }
}

/// <summary>A request body made of byte segments written in order, so large shared parts are never copied.</summary>
internal sealed class SegmentsContent(IReadOnlyList<ReadOnlyMemory<byte>> segments) : HttpContent
{
    protected override async Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) =>
        await SerializeToStreamAsync(stream, context, CancellationToken.None);

    protected override async Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context, CancellationToken cancellationToken)
    {
        foreach (var segment in segments)
        {
            await stream.WriteAsync(segment, cancellationToken);
        }
    }

    protected override bool TryComputeLength(out long length)
    {
        length = segments.Sum(s => (long)s.Length);
        return true;
    }
}

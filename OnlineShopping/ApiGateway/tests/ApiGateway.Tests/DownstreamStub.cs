using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ApiGateway.Tests;

public sealed class DownstreamStub : IAsyncDisposable
{
    private readonly WebApplication _application;
    private readonly ConcurrentDictionary<StubRoute, StubResponse> _responses = new();
    private readonly ConcurrentQueue<DownstreamRequest> _requests = new();
    private int _disposed;

    private DownstreamStub(WebApplication application)
    {
        _application = application;
    }

    public Uri BaseAddress { get; private set; } = null!;

    public IReadOnlyList<DownstreamRequest> Requests => _requests.ToArray();

    public int TotalRequestCount => _requests.Count;

    public static async Task<DownstreamStub> StartAsync(CancellationToken cancellationToken = default)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));

        WebApplication application = builder.Build();
        DownstreamStub stub = new(application);
        application.Run(stub.HandleRequestAsync);
        await application.StartAsync(cancellationToken).ConfigureAwait(false);

        IServer server = application.Services.GetRequiredService<IServer>();
        string address = server.Features.Get<IServerAddressesFeature>()?.Addresses.Single()
            ?? throw new InvalidOperationException("The downstream stub did not expose its loopback address.");
        stub.BaseAddress = new Uri(address);
        return stub;
    }

    public void SetResponse(HttpMethod method, string path, HttpStatusCode statusCode, string json, TimeSpan? delay = null)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(json);
        if (!path.StartsWith('/'))
        {
            throw new ArgumentException("The stub path must begin with '/'.", nameof(path));
        }

        if (delay is TimeSpan configuredDelay && configuredDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), "The response delay cannot be negative.");
        }

        _responses[new StubRoute(method.Method, path)] = new StubResponse(statusCode, json, delay);
    }

    public int GetRequestCount(HttpMethod method, string path)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return _requests.Count(request => request.Method == method && request.Path == path);
    }

    public void ResetRequests()
    {
        while (_requests.TryDequeue(out _))
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            await _application.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task HandleRequestAsync(HttpContext context)
    {
        string path = context.Request.Path.Value ?? "/";
        string method = context.Request.Method;
        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
        string body = await reader.ReadToEndAsync(context.RequestAborted).ConfigureAwait(false);
        string? authorization = context.Request.Headers.Authorization.FirstOrDefault();
        string? authorizationSha256 = authorization is null
            ? null
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(authorization)));
        var headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in context.Request.Headers)
        {
            headers[header.Key] = header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)
                ? ["<redacted>"]
                : header.Value.Where(value => value is not null).Select(value => value!).ToArray();
        }

        _requests.Enqueue(new DownstreamRequest(
            new HttpMethod(method),
            path,
            context.Request.QueryString.Value ?? string.Empty,
            body,
            headers,
            authorizationSha256));

        if (!_responses.TryGetValue(new StubRoute(method, path), out StubResponse? response))
        {
            response = new StubResponse(HttpStatusCode.NotFound, "{}");
        }

        if (response.Delay is TimeSpan delay && delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, context.RequestAborted).ConfigureAwait(false);
        }

        context.Response.StatusCode = (int)response.StatusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(response.Json, context.RequestAborted).ConfigureAwait(false);
    }

    private readonly record struct StubRoute(string Method, string Path);

    private sealed record StubResponse(HttpStatusCode StatusCode, string Json, TimeSpan? Delay = null);
}

public sealed record DownstreamRequest(
    HttpMethod Method,
    string Path,
    string Query,
    string Body,
    IReadOnlyDictionary<string, string[]> Headers,
    string? AuthorizationSha256);
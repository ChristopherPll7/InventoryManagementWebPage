using System.Net;
using System.Security.Claims;
using System.Text;
using InventoryManagement.Web.Models;
using InventoryManagement.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

internal static class TransportAcceptance
{
    public static async Task RunAsync()
    {
        var id = Guid.NewGuid();
        var passed = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception(name);
            Console.WriteLine("PASS " + name);
            passed++;
        }
        async Task<ApiCallException> Failure(Func<Task> action)
        {
            try { await action(); }
            catch (ApiCallException exception) { return exception; }
            throw new Exception("Expected controlled API error");
        }

        foreach (var status in new[] { 200, 400 })
        {
            using var probe = new Probe(status, new Body("", Timeout.InfiniteTimeSpan), TimeSpan.FromMilliseconds(150));
            var error = await Failure(() => probe.Api.GetCategoryAsync(id, default));
            Check(error.Code == "API_UNAVAILABLE" && probe.Body.Disposed && probe.Handler.Calls == 1,
                $"HTTP {status}: stalled body times out and is disposed");
        }
        foreach (var status in new[] { 200, 400 })
        foreach (var failure in new Exception[] { new IOException("broken stream"), new HttpRequestException("broken transport") })
        {
            using var probe = new Probe(status, new Body("", TimeSpan.Zero, failure), TimeSpan.FromSeconds(2));
            var error = await Failure(() => probe.Api.GetCategoryAsync(id, default));
            Check(error.Code == "API_UNAVAILABLE" && probe.Body.Disposed,
                $"HTTP {status}: body {failure.GetType().Name} becomes recoverable error");
        }
        using (var probe = new Probe(200, new Body("", Timeout.InfiniteTimeSpan), TimeSpan.FromSeconds(5)))
        using (var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(150)))
        {
            try { await probe.Api.GetCategoryAsync(id, cancellation.Token); throw new Exception("Expected cancellation"); }
            catch (OperationCanceledException)
            {
                Check(probe.Body.Disposed && probe.Logger.Messages.Count == 0, "caller cancellation propagates without timeout logging");
            }
        }
        using (var probe = new Probe(201, new Body($"{{\"id\":\"{id}\"}}", TimeSpan.FromMilliseconds(300)), TimeSpan.FromMilliseconds(400), TimeSpan.FromMilliseconds(250)))
        {
            var error = await Failure(() => probe.Api.CreateCategoryAsync(new CategoryRequest("Test", null), default));
            Check(error.Code == "API_UNAVAILABLE" && probe.Handler.Calls == 1 && probe.Body.Disposed,
                "one deadline covers headers plus body; uncertain POST is not retried");
        }
        using (var probe = new Probe(201, new Body($"{{\"id\":\"{id}\"}}", TimeSpan.FromMilliseconds(10)), TimeSpan.FromSeconds(2)))
        {
            var result = await probe.Api.CreateCategoryAsync(new CategoryRequest("Test", null), default);
            Check(result.Id == id && probe.Body.Disposed, "valid success body deserializes and is disposed");
        }
        using (var probe = new Probe(409, new Body("{\"code\":\"DUPLICATE_CATEGORY_NAME\",\"message\":\"Conflict\"}", TimeSpan.Zero), TimeSpan.FromSeconds(2)))
        {
            var error = await Failure(() => probe.Api.CreateCategoryAsync(new CategoryRequest("Test", null), default));
            Check(error.StatusCode == 409 && error.Code == "DUPLICATE_CATEGORY_NAME", "business error contract preserved");
        }
        using (var probe = new Probe(200, new Body("invalid-json", TimeSpan.Zero), TimeSpan.FromSeconds(2)))
        {
            var error = await Failure(() => probe.Api.GetCategoryAsync(id, default));
            Check(error.StatusCode == 502 && probe.Body.Disposed, "invalid JSON remains controlled 502");
        }
        using (var probe = new Probe(500, new Body("INTERNAL_SECRET_STACK_TRACE", Timeout.InfiniteTimeSpan), TimeSpan.FromSeconds(2)))
        {
            var error = await Failure(() => probe.Api.GetCategoryAsync(id, default));
            Check(error.StatusCode == 500 && error.Code == "INTERNAL_ERROR" && !error.Message.Contains("SECRET")
                && probe.Logger.Messages.Any(m => m.Contains("500") && m.Contains("GET"))
                && probe.Logger.Messages.All(m => !m.Contains("SECRET")) && probe.Body.Reads == 0 && probe.Body.Disposed,
                "HTTP 500 logs status/method/path, hides upstream body and disposes it without waiting");
        }
        using (var probe = new Probe(204, new Body("", Timeout.InfiniteTimeSpan), TimeSpan.FromSeconds(2)))
        {
            await probe.Api.DeleteCategoryAsync(id, default);
            Check(probe.Body.Reads == 0 && probe.Body.Disposed, "204 does not read a body and disposes response");
        }
        Console.WriteLine($"{passed} transport checks passed with controlled HTTP streams.");
    }

    internal sealed class Probe : IDisposable
    {
        private readonly ServiceProvider services;
        private readonly HttpClient http;
        public InventoryApiClient Api { get; }
        public Body Body { get; }
        public Handler Handler { get; }
        public CaptureLogger Logger { get; } = new();
        public Probe(int status, Body body, TimeSpan timeout, TimeSpan headersDelay = default)
        {
            Body = body;
            Handler = new Handler(status, body, headersDelay);
            http = new HttpClient(Handler) { BaseAddress = new Uri("http://api.test/"), Timeout = timeout };
            var properties = new AuthenticationProperties();
            properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = "test-token" },
                new AuthenticationToken { Name = "expires_at", Value = DateTimeOffset.UtcNow.AddHours(1).ToString("O") }]);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity("test")), properties, "Cookies");
            services = new ServiceCollection().AddSingleton<IAuthenticationService>(new TestAuthentication(ticket)).BuildServiceProvider();
            var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { RequestServices = services } };
            var session = new TokenSessionService(http, accessor, new ConfigurationBuilder().Build(), NullLogger<TokenSessionService>.Instance);
            Api = new InventoryApiClient(http, session, Logger);
        }
        public void Dispose() { http.Dispose(); services.Dispose(); }
    }
    internal sealed class Handler(int status, Body body, TimeSpan delay) : HttpMessageHandler
    {
        public Uri? Location { get; set; }
        public int Calls { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            await Task.Delay(delay, ct);
            var response = new HttpResponseMessage((HttpStatusCode)status) { Content = new StreamContent(body) };
            response.Headers.Location = Location;
            return response;
        }
    }
    internal sealed class Body(string json, TimeSpan delay, Exception? failure = null) : MemoryStream(Encoding.UTF8.GetBytes(json))
    {
        public bool Disposed { get; private set; }
        public int Reads { get; private set; }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Reads++;
            await Task.Delay(delay, cancellationToken);
            if (failure is not null) throw failure;
            return base.Read(buffer.Span);
        }
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            await ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
    internal sealed class CaptureLogger : ILogger<InventoryApiClient>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}

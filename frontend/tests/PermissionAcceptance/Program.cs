using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using InventoryManagement.Web.Controllers;
using InventoryManagement.Web.Models;
using InventoryManagement.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

var id = Guid.NewGuid();
var passed = 0;
void Check(bool condition, string scenario)
{
    if (!condition) throw new Exception(scenario);
    Console.WriteLine($"PASS {scenario}");
    passed++;
}
ProductForm Product() => new() { Name = "Product", Sku = "SKU", PriceText = "1.00", CategoryId = id };

using (var env = new Scenario("inventory.read"))
{
    var controller = env.Attach(new InventoryController(env.Api, env.Permissions));
    var result = (ViewResult)await controller.Details(id, new HistoryFilter(), default);
    Check(result.Model is InventoryPage { IsActive: null }, "inventory.read renders without product status");
    Check(env.Calls.Count == 2 && env.Calls.All(c => c.Contains("api/inventory/")), "inventory.read makes only balance/history calls");
}
using (var env = new Scenario("inventory.write"))
{
    var controller = env.Attach(new InventoryController(env.Api, env.Permissions));
    Check(await controller.Movement(id) is ViewResult && env.Calls.Count == 0, "movement form needs no GET permission");
    var result = (RedirectToActionResult)await controller.Register(id, new MovementForm { Type = "Entry", Quantity = 1 }, default);
    Check(result.ActionName == "Movement" && env.Calls.Count == 1 && env.Calls[0].StartsWith("POST"), "inventory.write submits without reads and redirects to form");
}
foreach (var code in new[] { "INSUFFICIENT_STOCK", "PRODUCT_INACTIVE" })
{
    using var env = new Scenario("inventory.write") { MovementError = code };
    var controller = env.Attach(new InventoryController(env.Api, env.Permissions));
    var form = new MovementForm { Type = "Exit", Quantity = 2, Reason = "Keep this" };
    var result = (ViewResult)await controller.Register(id, form, default);
    Check(ReferenceEquals(result.Model, form) && !controller.ModelState.IsValid && env.Calls.Count == 1,
        $"write-only {code} preserves form without reads");
    if (code == "PRODUCT_INACTIVE") Check(controller.ViewData["Inactive"] is true, "inactive conflict blocks movement form");
}
using (var env = new Scenario("inventory.read", "inventory.write") { MovementError = "INSUFFICIENT_STOCK" })
{
    var controller = env.Attach(new InventoryController(env.Api, env.Permissions));
    var result = (ViewResult)await controller.Register(id, new MovementForm { Type = "Exit", Quantity = 2 }, default);
    Check(result.Model is InventoryPage && env.Calls.Count == 3, "stock conflict refreshes balance/history with inventory.read only");
}
using (var env = new Scenario("inventory.read", "products.read"))
{
    env.Inactive = true;
    var controller = env.Attach(new InventoryController(env.Api, env.Permissions));
    var result = (ViewResult)await controller.Details(id, new HistoryFilter(), default);
    Check(result.Model is InventoryPage { IsActive: false }, "known inactive product stays consultable and blocked for movement");
}
using (var env = new Scenario("products.write"))
{
    var controller = env.Attach(new ProductsController(env.Api, env.Permissions));
    Check(await controller.Create(default(CancellationToken)) is ViewResult && controller.ViewData["ManualCategory"] is true,
        "product creation tolerates categories.read 403 and offers UUID");
    var result = (RedirectResult)await controller.Create(Product(), default);
    Check(result.Url == "/products" && env.Calls.Last().StartsWith("POST"), "products.write creates without post-success GET");
    Check(await controller.Edit(id, default(CancellationToken)) is ViewResult && controller.ViewData["WriteOnly"] is true,
        "products.write opens replacement form despite product GET 403");
    Check(await controller.Edit(id, Product(), default) is RedirectResult, "products.write updates without read permission");
    var confirmation = (ViewResult)await controller.Deactivate(id, default);
    Check(confirmation.Model is DeactivateById, "product deactivation can confirm UUID without product GET");
    Check(await controller.DeactivateConfirmed(id, default) is RedirectResult, "products.write deactivates without post-success GET");
}
using (var env = new Scenario("products.write"))
{
    var controller = env.Attach(new ProductsController(env.Api, env.Permissions));
    var form = Product();
    controller.ModelState.AddModelError("PriceText", "Invalid");
    var result = (ViewResult)await controller.Create(form, default);
    Check(ReferenceEquals(result.Model, form) && controller.ViewData["ManualCategory"] is true && env.Calls.All(c => c.StartsWith("GET")),
        "invalid product form preserved when category lookup is forbidden");
}
using (var env = new Scenario("categories.write"))
{
    var controller = env.Attach(new CategoriesController(env.Api, env.Permissions));
    Check(await controller.Create(new CategoryForm { Name = "Category" }, default) is RedirectResult, "categories.write creates without post-success read");
    Check(await controller.Edit(id, default(CancellationToken)) is ViewResult && controller.ViewData["WriteOnly"] is true,
        "categories.write opens replacement form despite GET 403");
    Check(await controller.Edit(id, new CategoryForm { Name = "Changed" }, default) is RedirectResult, "categories.write updates independently");
    Check(((ViewResult)await controller.Deactivate(id, default)).Model is DeactivateById, "category confirmation by UUID after GET 403");
    Check(await controller.DeactivateConfirmed(id, default) is RedirectResult, "categories.write deactivates independently");
}
foreach (var resource in new[] { "categories", "products", "inventory" })
{
    using var env = new Scenario(resource + ".read");
    Controller controller;
    IActionResult result;
    if (resource == "categories")
    {
        var c = env.Attach(new CategoriesController(env.Api, env.Permissions)); controller = c;
        result = await c.Create(new CategoryForm { Name = "Denied" }, default);
    }
    else if (resource == "products")
    {
        var c = env.Attach(new ProductsController(env.Api, env.Permissions)); controller = c;
        result = await c.Create(Product(), default);
    }
    else
    {
        var c = env.Attach(new InventoryController(env.Api, env.Permissions)); controller = c;
        result = await c.Register(id, new MovementForm { Quantity = 1 }, default);
    }
    Check(result is ViewResult && controller.Response.StatusCode == 403 && !controller.TempData.ContainsKey("Success"),
        $"API denies {resource} write for read-only user; no false success");
}
Console.WriteLine($"{passed} checks passed. Real controllers/client with simulated API/authentication; not Keycloak end-to-end.");
foreach (var readPermission in new[] { "products.read", "inventory.read" })
{
    using var env = new Scenario("inventory.write", readPermission);
    var controller = env.Attach(new InventoryController(env.Api, env.Permissions));
    var result = await controller.Movement(id);
    Check(result is ViewResult && (string?)controller.ViewData["ProductName"] == "Product"
        && (Guid)controller.ViewData["ProductId"]! == id && env.Calls.Count == 1,
        "movement shows optional product name with " + readPermission + " and preserves UUID");
    controller.ModelState.AddModelError("Quantity", "Invalid quantity");
    await controller.Register(id, new MovementForm(), default);
    Check(!env.Calls.Any(call => call.StartsWith("POST")), "invalid movement does not submit while rebuilding display metadata");
}
await TransportAcceptance.RunAsync();
await FormAcceptance.RunAsync();
await PaginationAcceptance.RunAsync();
await ResponseContractAcceptance.RunAsync();
await DirectAccessAcceptance.RunAsync();
try { await HttpFlowAcceptance.RunAsync(); }
catch (Exception error) { Console.Error.WriteLine(error); Environment.ExitCode = 1; }

sealed class Scenario : HttpMessageHandler
{
    private readonly HashSet<string> permissions;
    private readonly ServiceProvider services;
    private readonly HttpClient http;
    public DefaultHttpContext Context { get; }
    public InventoryApiClient Api { get; }
    public PermissionService Permissions { get; }
    public List<string> Calls { get; } = [];
    public List<string> RequestUrls { get; } = [];
    public string? MovementError { get; set; }
    public bool Inactive { get; set; }
    public string? FailureCall { get; set; }
    public int FailureStatus { get; set; }
    public const string FixtureId = "319493f0-ed4a-4a5e-96c2-b0a20afdc157";
    public ApiError? WriteError { get; set; }
    public int WriteErrorStatus { get; set; } = 400;
    public Scenario(params string[] permissions)
    {
        this.permissions = permissions.ToHashSet();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "test")], "test"));
        var properties = new AuthenticationProperties();
        var token = "test." + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { permissions }))).TrimEnd('=').Replace('+', '-').Replace('/', '_') + ".test";
        properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = token },
            new AuthenticationToken { Name = "expires_at", Value = DateTimeOffset.UtcNow.AddHours(1).ToString("O") }]);
        services = new ServiceCollection().AddSingleton<IAuthenticationService>(new TestAuthentication(new AuthenticationTicket(principal, properties, "Cookies"))).BuildServiceProvider();
        Context = new DefaultHttpContext { User = principal, RequestServices = services };
        var accessor = new HttpContextAccessor { HttpContext = Context };
        http = new HttpClient(this, false) { BaseAddress = new Uri("http://api.test/") };
        var session = new TokenSessionService(http, accessor, new ConfigurationBuilder().Build(), NullLogger<TokenSessionService>.Instance);
        Api = new InventoryApiClient(http, session, NullLogger<InventoryApiClient>.Instance);
        Permissions = new PermissionService(accessor);
    }
    public T Attach<T>(T controller) where T : Controller
    {
        controller.ControllerContext = new ControllerContext { HttpContext = Context, RouteData = new Microsoft.AspNetCore.Routing.RouteData(), ActionDescriptor = new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor() };
        controller.Url = new Microsoft.AspNetCore.Mvc.Routing.UrlHelper(controller.ControllerContext);
        controller.TempData = new TempDataDictionary(Context, new TestTempData());
        return controller;
    }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.AbsolutePath;
        Calls.Add($"{request.Method} {path}");
        RequestUrls.Add(request.RequestUri.PathAndQuery);
        if (FailureCall == $"{request.Method} {path}")
            return Reply(FailureStatus, new ApiError(FailureStatus == 403 ? "FORBIDDEN" : "TEST_ERROR", FailureStatus >= 500 ? "PRIVATE_BACKEND_DETAIL" : "Business failure"));
        var resource = path.Split('/')[2];
        var permission = resource + (request.Method == HttpMethod.Get ? ".read" : ".write");
        if (!permissions.Contains(permission)) return Reply(403, new ApiError("FORBIDDEN", "Permission denied"));
        if (request.Method == HttpMethod.Get && path == "/api/categories")
            return Reply(200, new PagedResult<CategoryDto>([new CategoryDto(Guid.Parse(FixtureId), "Category", null, true, DateTimeOffset.UtcNow, null)], 1, 20, 1, 1));
        if (request.Method == HttpMethod.Get && path == "/api/products")
            return Reply(200, new PagedResult<ProductDto>([new ProductDto(Guid.Parse(FixtureId), "Product", null, "SKU", 1m, Guid.Parse(FixtureId), true, 1)], 1, 20, 1, 1));
        if (request.Method != HttpMethod.Get && WriteError is not null) return Reply(WriteErrorStatus, WriteError);
        if (request.Method == HttpMethod.Delete) return Reply(204, null);
        if (request.Method == HttpMethod.Post)
        {
            if (resource == "inventory") return MovementError is null
                ? Reply(201, new InventoryMovementResult(Guid.NewGuid(), 1)) : Reply(409, new ApiError(MovementError, "Conflict"));
            return Reply(201, new CreatedId(Guid.NewGuid()));
        }
        var id = Guid.TryParse(path.Split('/').Last(), out var parsed) ? parsed : Guid.NewGuid();
        if (resource == "inventory") return path.EndsWith("movements")
            ? Reply(200, new PagedResult<InventoryMovementDto>([], 1, 20, 0, 0))
            : Reply(200, new ProductInventoryDto(id, "SKU", "Product", 1));
        if (resource == "products") return Reply(200, new ProductDto(id, "Product", null, "SKU", 1m, Guid.NewGuid(), !Inactive, 1));
        return Reply(200, new CategoryDto(id, "Category", null, true, DateTimeOffset.UtcNow, null));
    }
    private static Task<HttpResponseMessage> Reply(int status, object? value) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)
    { Content = value is null ? null : JsonContent.Create(value) });
    protected override void Dispose(bool disposing) { if (disposing) { http.Dispose(); services.Dispose(); } base.Dispose(disposing); }
}
sealed class TestAuthentication(AuthenticationTicket ticket) : IAuthenticationService
{
    public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => Task.FromResult(AuthenticateResult.Success(ticket));
    public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => throw new NotSupportedException();
    public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => throw new NotSupportedException();
    public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => throw new NotSupportedException();
    public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => throw new NotSupportedException();
}
sealed class TestTempData : ITempDataProvider
{
    public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
    public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
}

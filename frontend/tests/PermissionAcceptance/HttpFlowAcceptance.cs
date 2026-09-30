using System.Net;
using System.Text.RegularExpressions;
using InventoryManagement.Web.Controllers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Real HTTP routing, Razor, model binding, redirects and antiforgery; simulated API/auth.
internal static class HttpFlowAcceptance
{
    public static async Task RunAsync()
    {
        using var env = new Scenario("categories.read", "categories.write", "products.read", "products.write", "inventory.read", "inventory.write");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(InventoryController).Assembly.GetName().Name,
            EnvironmentName = "Development"
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))
            .AddApplicationPart(typeof(InventoryController).Assembly);
        builder.Services.AddAuthentication();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton(env.Context.RequestServices.GetRequiredService<IAuthenticationService>());
        builder.Services.AddSingleton(env.Api);
        builder.Services.AddSingleton(env.Permissions);
        await using var app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.Use(async (context, next) => { context.User = env.Context.User; await next(context); });
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Urls.Single()) };
        var id = Scenario.FixtureId;
        var count = 0;
        void Check(bool value, string message)
        {
            if (!value) throw new Exception("HTTP FLOW: " + message);
            count++;
            Console.WriteLine("PASS HTTP " + message);
        }
        var cases = new (string Api, string Page, bool Post, string? Destination)[]
        {
            ("GET /api/categories", "/categories", false, null),
            ($"GET /api/categories/{id}", $"/categories/{id}", false, null),
            ("POST /api/categories", "/categories/create", true, "/categories/"),
            ($"PUT /api/categories/{id}", $"/categories/{id}/edit", true, $"/categories/{id}"),
            ($"DELETE /api/categories/{id}", $"/categories/{id}/deactivate", true, $"/categories/{id}"),
            ("GET /api/products", "/products", false, null),
            ($"GET /api/products/{id}", $"/products/{id}", false, null),
            ("POST /api/products", "/products/create", true, "/products/"),
            ($"PUT /api/products/{id}", $"/products/{id}/edit", true, $"/products/{id}"),
            ($"DELETE /api/products/{id}", $"/products/{id}/deactivate", true, $"/products/{id}"),
            ($"GET /api/inventory/products/{id}", $"/inventory/products/{id}", false, null),
            ($"GET /api/inventory/products/{id}/movements", $"/inventory/products/{id}", false, null),
            ("POST /api/inventory/movements", $"/inventory/products/{id}/movements", true, $"/inventory/products/{id}")
        };
        try
        {
            foreach (var item in cases)
            foreach (var status in new[] { 0, 400, 401, 403, 404, 409, 500 })
            {
                env.FailureCall = null;
                string? action = null;
                var fields = new Dictionary<string, string>
                {
                    ["Name"] = "Product", ["Description"] = "Description", ["Sku"] = "SKU",
                    ["PriceText"] = "1.00", ["CategoryId"] = id, ["Type"] = "Entry", ["Quantity"] = "1"
                };
                if (item.Post)
                {
                    var html = await client.GetStringAsync(item.Page);
                    var form = Regex.Matches(html, "<form\\b[^>]*>.*?</form>", RegexOptions.Singleline | RegexOptions.IgnoreCase)
                        .Cast<Match>().Select(m => m.Value).First(f => f.Contains("action=\"" + item.Page));
                    action = WebUtility.HtmlDecode(Regex.Match(form, "action=\"([^\"]+)\"").Groups[1].Value);
                    fields["__RequestVerificationToken"] = WebUtility.HtmlDecode(Regex.Match(form, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
                    Check(!action.Contains('@') && fields["__RequestVerificationToken"].Length > 0, item.Api + " rendered form action and antiforgery");
                }
                env.Calls.Clear();
                env.FailureCall = status == 0 ? null : item.Api;
                env.FailureStatus = status;
                using var response = item.Post
                    ? await client.PostAsync(action, new FormUrlEncodedContent(fields))
                    : await client.GetAsync(item.Page);
                var body = await response.Content.ReadAsStringAsync();
                Check(env.Calls.Contains(item.Api), item.Api + " reached through MVC (" + response.StatusCode + ")");
                if (status == 0 && item.Post)
                {
                    var location = response.Headers.Location?.OriginalString;
                    Check(response.StatusCode == HttpStatusCode.Redirect && location?.StartsWith(item.Destination!) == true,
                        item.Api + " success redirect");
                    using var destination = await client.GetAsync(location);
                    Check(destination.StatusCode == HttpStatusCode.OK, item.Api + " destination renders successfully");
                }
                else if (status == 401)
                    Check(response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location?.OriginalString.StartsWith("/login?") == true,
                        item.Api + " expired session redirects to login");
                else
                {
                    var correctable = item.Post && !item.Api.StartsWith("DELETE")
                        && (status is 400 or 409 || status == 404 && item.Page.EndsWith("/create"));
                    var expected = status == 0 || correctable ? 200 : status;
                    Check((int)response.StatusCode == expected && !body.Contains("PRIVATE_BACKEND_DETAIL"),
                        item.Api + " status " + status + " renders expected " + expected + " without leaking internal detail");
                    if (correctable)
                        Check(body.Contains("TEST_ERROR") && body.Contains("<form"), item.Api + " business error retains correction form");
                }
            }
            env.FailureCall = null;
            var queryHtml = await client.GetStringAsync($"/inventory/products/{id}/movements?returnUrl=%2Fproducts%3Fpage%3D2");
            Check(queryHtml.Contains($"/inventory/products/{id}/movements?returnUrl=%2Fproducts%3Fpage%3D2")
                && !queryHtml.Contains("@Context.Request.QueryString"), "movement action preserves encoded return query in rendered HTML");
            env.Calls.Clear();
            using var noToken = await client.PostAsync($"/inventory/products/{id}/movements", new FormUrlEncodedContent(new Dictionary<string,string> { ["Quantity"] = "1" }));
            Check(noToken.StatusCode == HttpStatusCode.BadRequest && env.Calls.Count == 0, "missing antiforgery blocks API write");
            Console.WriteLine($"{count} HTTP/Razor flow checks passed; API and authentication simulated.");
        }
        finally { await app.StopAsync(); }
    }
}

using InventoryManagement.Web;
using InventoryManagement.Web.Controllers;
using InventoryManagement.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.WebUtilities;

internal static class PaginationAcceptance
{
    public static async Task RunAsync()
    {
        var count = 0;
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); count++; Console.WriteLine("PASS " + message); }
        var id = Guid.NewGuid();
        foreach (var resource in new[] { "categories", "products" })
        {
            using var env = new Scenario(resource + ".read");
            UiController controller = resource == "categories" ? env.Attach(new CategoriesController(env.Api, env.Permissions)) : env.Attach(new ProductsController(env.Api, env.Permissions));
            controller.ModelState.SetModelValue("page", "abc", "abc");
            controller.ModelState.AddModelError("page", "Invalid number");
            var result = resource == "categories" ? await ((CategoriesController)controller).Index() : await ((ProductsController)controller).Index();
            Check(result is ViewResult { Model: HistoryFilter } && controller.Response.StatusCode == 400 && env.Calls.Count == 0
                && controller.ModelState["page"]!.AttemptedValue == "abc", resource + " invalid query binding preserves value without API call");
        }
        Check(new PagerModel(999, 2000, 40000, p => p.ToString()).HasNext && !new PagerModel(1000, 2000, 40000, p => p.ToString()).HasNext, "next stops at page 1000");
        Check(!new PagerModel(5, 5, 100, p => p.ToString()).HasNext, "next stops at API last page");
        Check(!PaginationLimits.IsInvalid(1000, 100) && PaginationLimits.IsInvalid(1001, 20), "UI validates page 1000 boundary consistently");
        foreach (var filter in new[] { new HistoryFilter { Type = "invalid" }, new HistoryFilter { StartDateText = "not-a-date" }, new HistoryFilter { Page = 1001 },
            new HistoryFilter { StartDateText = "2026-10-02T00:00:00Z", EndDateText = "2026-10-01T00:00:00Z" } })
        {
            using var env = new Scenario("inventory.read");
            var controller = env.Attach(new InventoryController(env.Api, env.Permissions));
            var result = (ViewResult)await controller.Details(id, filter, default);
            Check(ReferenceEquals(result.Model, filter) && env.Calls.Count == 0 && controller.Response.StatusCode == 400, "invalid inventory filters never query replacement defaults");
        }
        using (var env = new Scenario("inventory.read"))
        {
            var controller = env.Attach(new InventoryController(env.Api, env.Permissions));
            controller.ModelState.AddModelError("PageSize", "Not numeric");
            var result = await controller.Details(id, new HistoryFilter(), default);
            Check(result is ViewResult { Model: HistoryFilter } && env.Calls.Count == 0, "inventory honors query ModelState errors");
        }
        foreach (var conflict in new[] { false, true })
        {
            using var env = new Scenario("inventory.read", "inventory.write") { MovementError = conflict ? "INSUFFICIENT_STOCK" : null };
            var controller = env.Attach(new InventoryController(env.Api, env.Permissions));
            var filter = new HistoryFilter { Type = "Entry", Page = 3, PageSize = 50, StartDateText = "2026-09-01T00:00:00+02:00" };
            var result = await controller.Register(id, new MovementForm { Type = "Exit", Quantity = 1 }, default, filter);
            if (conflict) Check(result is ViewResult { Model: InventoryPage page } && ReferenceEquals(page.Filter, filter)
                && env.RequestUrls.Any(url => url.Contains("page=3&pageSize=50") && url.Contains("type=Entry") && url.Contains("%2B02")), "movement conflict preserves history filters and encoded offset");
            else Check(result is RedirectToActionResult redirect && Equals(redirect.RouteValues!["Page"], 3) && Equals(redirect.RouteValues["Type"], "Entry"), "movement success redirects with original history filters");
        }
        using (var env = new Scenario("products.read"))
        {
            var controller = env.Attach(new ProductsController(env.Api, env.Permissions));
            env.Context.Request.Path = "/products";
            env.Context.Request.QueryString = new QueryString("?page=4&pageSize=50");
            var view = new ViewContext { HttpContext = env.Context, RouteData = controller.RouteData, ActionDescriptor = controller.ControllerContext.ActionDescriptor };
            var helper = new NavigationTagHelper(new UrlHelperFactory()) { ViewContext = view };
            TagHelperOutput Output(string attribute, string target) => new("a", new TagHelperAttributeList { { attribute, "" }, { "href", target } }, (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
            var context = new TagHelperContext(new TagHelperAttributeList(), new Dictionary<object, object>(), "test");
            var link = Output("preserve-return", $"/products/{id}"); helper.Process(context, link);
            var target = link.Attributes["href"].Value.ToString()!;
            Check(QueryHelpers.ParseQuery(new Uri("http://local" + target).Query)["returnUrl"] == "/products?page=4&pageSize=50", "detail link carries list page and size");
            env.Context.Request.QueryString = new QueryString("?returnUrl=" + Uri.EscapeDataString("/products?page=4&pageSize=50"));
            var back = Output("return-back", "/products"); helper.Process(context, back);
            Check(back.Attributes["href"].Value.ToString() == "/products?page=4&pageSize=50", "back link restores exact local query");
            env.Context.Request.QueryString = new QueryString("?returnUrl=https%3A%2F%2Fevil.example");
            back = Output("return-back", "/products"); helper.Process(context, back);
            Check(back.Attributes["href"].Value.ToString() == "/products", "external return URL ignored");
        }
        Console.WriteLine($"{count} pagination/navigation checks passed with controlled API and tag helpers.");
    }
}

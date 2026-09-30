using InventoryManagement.Web.Controllers;
using InventoryManagement.Web.Models;
using Microsoft.AspNetCore.Mvc;

internal static class DirectAccessAcceptance
{
    public static async Task RunAsync()
    {
        var count = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message); count++; Console.WriteLine("PASS " + message); }
        foreach (var category in new[] { true, false })
        {
            foreach (var operation in new DirectAccessForm { Category = category }.Operations)
            {
                using var env = new Scenario(operation.Permission);
                var categories = env.Attach(new CategoriesController(env.Api, env.Permissions));
                var products = env.Attach(new ProductsController(env.Api, env.Permissions));
                var page = (ViewResult)(category ? await categories.Index() : await products.Index());
                Check(page.Model is DirectAccessForm && env.Calls.Count == 0, operation.Value + " independent access without catalog calls");
                var id = Guid.NewGuid();
                var form = new DirectAccessForm { Id = id.ToString(), Operation = operation.Value, Category = !category };
                var result = (RedirectResult)(category ? await categories.Open(form) : await products.Open(form));
                Check(result.Url == operation.Prefix + id + operation.Suffix && env.Calls.Count == 0, "UUID navigation ignores client resource flag and makes no lookup");
                foreach (var invalid in new[] { "", "not-a-uuid", Guid.Empty.ToString() })
                {
                    categories.ModelState.Clear(); products.ModelState.Clear();
                    form.Id = invalid;
                    var rejected = (ViewResult)(category ? await categories.Open(form) : await products.Open(form));
                    Check(ReferenceEquals(rejected.Model, form) && !(category ? categories.ModelState : products.ModelState).IsValid && env.Calls.Count == 0, "invalid UUID preserves form without API call");
                }
            }
            using var denied = new Scenario();
            var c = denied.Attach(new CategoriesController(denied.Api, denied.Permissions));
            var p = denied.Attach(new ProductsController(denied.Api, denied.Permissions));
            var request = new DirectAccessForm { Id = Guid.NewGuid().ToString(), Operation = "edit" };
            Check((category ? await c.Open(request) : await p.Open(request)) is ViewResult && denied.Context.Response.StatusCode == 403 && denied.Calls.Count == 0, "unauthorized action denied");
            request.Operation = "https://external.test";
            Check((category ? await c.Open(request) : await p.Open(request)) is BadRequestResult, "unknown action rejected");
        }
        Console.WriteLine($"{count} direct UUID access checks passed.");
    }
}

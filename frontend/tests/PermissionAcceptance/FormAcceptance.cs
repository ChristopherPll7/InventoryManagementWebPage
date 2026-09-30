using System.ComponentModel.DataAnnotations;
using InventoryManagement.Web.Controllers;
using InventoryManagement.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

internal static class FormAcceptance
{
    public static async Task RunAsync()
    {
        var passed = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS " + message); passed++; }
        bool Valid(object form) => Validator.TryValidateObject(form, new ValidationContext(form), new List<ValidationResult>(), true);
        var id = Guid.NewGuid();
        ProductForm Product() => new() { Name = "Product", Description = "Keep description", Sku = "SKU", PriceText = "1.00", CategoryId = id };
        var category = new CategoryForm { Name = " " + new string('a', 150) + " ", Description = " " + new string('b', 500) + " " };
        Check(Valid(category) && category.ToRequest().Name.Length == 150 && category.ToRequest().Description!.Length == 500, "category limits use trimmed values");
        category.Description = new string('b', 501);
        Check(!Valid(category), "category rejects trimmed description above limit");
        category.Description = null; category.Name = " ";
        Check(!Valid(category), "whitespace-only category name rejected");
        var product = Product(); product.Name = " " + new string('a', 150) + " "; product.Sku = " " + new string('s', 80) + " "; product.Description = " " + new string('d', 1000) + " ";
        Check(Valid(product) && product.ToRequest().Description!.Length == 1000, "product accepts padded boundary values");
        product.Description = new string('d', 1001);
        Check(!Valid(product), "product rejects trimmed description above limit");
        product = Product(); product.CategoryId = Guid.Empty;
        Check(!Valid(product), "empty category UUID rejected");
        var movement = new MovementForm { Quantity = 1, Reason = " " + new string('r', 500) + " " };
        Check(Valid(movement) && movement.ToRequest(id).Reason!.Length == 500, "movement reason trimmed before length limit");
        movement.Reason = new string('r', 501);
        Check(!Valid(movement), "movement rejects trimmed reason above limit");
        Check(new CategoryForm { Name = "Test", Description = null }.ToRequest().Description is null
            && new CategoryForm { Name = "Test", Description = "  " }.ToRequest().Description == "", "optional null and whitespace mapping retained");
        foreach (var code in new[] { "CATEGORY_NOT_FOUND", "PRODUCT_NOT_FOUND", "INVALID_PRODUCT_DESCRIPTION" })
        {
            using var env = new Scenario("products.write") { WriteError = new ApiError(code, "Rejected"), WriteErrorStatus = code.EndsWith("NOT_FOUND") ? 404 : 400 };
            var controller = env.Attach(new ProductsController(env.Api, env.Permissions));
            var form = Product();
            var result = (ViewResult)await controller.Edit(id, form, default);
            if (code == "PRODUCT_NOT_FOUND") Check(result.Model is ErrorPage && controller.Response.StatusCode == 404, "missing product retains resource-not-found behavior");
            else Check(ReferenceEquals(result.Model, form) && controller.ModelState[code == "CATEGORY_NOT_FOUND" ? "CategoryId" : "Description"]!.Errors.Count > 0
                && form.Description == "Keep description", code + " stays on form and marks correct field");
        }
        using (var env = new Scenario("categories.write") { WriteError = new ApiError("INVALID_CATEGORY_DESCRIPTION", "Rejected") })
        {
            var controller = env.Attach(new CategoriesController(env.Api, env.Permissions));
            var form = new CategoryForm { Name = "Category", Description = "Keep" };
            var result = (ViewResult)await controller.Create(form, default);
            Check(ReferenceEquals(result.Model, form) && controller.ModelState["Description"]!.Errors.Count > 0, "category description error is attached to Description");
        }
        using (var env = new Scenario("inventory.read", "inventory.write"))
        {
            var controller = env.Attach(new InventoryController(env.Api, env.Permissions));
            controller.ModelState.SetModelValue("Quantity", "1.5", "1.5");
            controller.ModelState.AddModelError("Quantity", "Must be an integer");
            await controller.Register(id, new MovementForm { Reason = "Keep" }, default);
            Check(controller.ModelState["Quantity"]!.AttemptedValue == "1.5" && env.Calls.All(c => c.StartsWith("GET")), "invalid quantity retained in ModelState without POST");
            var count = env.Calls.Count;
            await controller.Register(Guid.Empty, new MovementForm { Quantity = 1 }, default);
            Check(env.Calls.Count == count, "empty product UUID makes no API call, even with inventory.read");
        }
        using (var env = new Scenario("categories.write"))
        {
            var controller = env.Attach(new CategoriesController(env.Api, env.Permissions));
            var context = new ActionExecutingContext(controller.ControllerContext, new List<IFilterMetadata>(), new Dictionary<string, object?> { ["id"] = Guid.Empty }, controller);
            controller.OnActionExecuting(context);
            Check(context.Result is ViewResult && controller.Response.StatusCode == 400 && env.Calls.Count == 0, "MVC action filter rejects empty route UUID before API access");
        }
        Console.WriteLine($"{passed} form checks passed; model/controller tests, not browser end-to-end.");
    }
}

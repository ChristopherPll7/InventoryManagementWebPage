using System.Globalization;
using InventoryManagement.Web.Models;
using InventoryManagement.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Web.Controllers;

[Authorize]
[Route("products")]
public sealed class ProductsController(InventoryApiClient api, PermissionService permissions) : UiController
{
    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        if (InvalidPagination(page, pageSize)) ModelState.AddModelError("", "La página debe estar entre 1 y 1000 y el tamaño entre 1 y 100.");
        if (!ModelState.IsValid) return QueryFailure(new HistoryFilter { Page = page, PageSize = pageSize });
        if (!await permissions.HasAsync("products.read"))
            return View("~/Views/Shared/DirectAccess.cshtml", new DirectAccessForm { Category = false });
        try
        {
            var products = await api.GetProductsAsync(page, pageSize, ct);
            IReadOnlyDictionary<Guid, string> names;
            try { names = (await api.GetAllCategoriesAsync(ct)).ToDictionary(category => category.Id, category => category.Name); }
            catch (ApiCallException exception) when (exception.StatusCode == 403) { names = new Dictionary<Guid, string>(); }
            return View(new ProductListPage(products, names));
        }
        catch (ApiCallException exception) when (exception.StatusCode == 400)
        {
            ModelState.AddModelError("", "La API rechazó la paginación. Revisa la página y el tamaño.");
            return QueryFailure(new HistoryFilter { Page = page, PageSize = pageSize });
        }
        catch (ApiCallException exception) { return Failure(exception, "/products"); }
    }

    [HttpGet("open")]
    public Task<IActionResult> Open(DirectAccessForm form) => OpenById(form, false, permissions);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        try
        {
            var product = await api.GetProductAsync(id, ct);
            string name;
            try { name = (await api.GetCategoryAsync(product.CategoryId, ct)).Name; }
            catch (ApiCallException exception) when (exception.StatusCode is 403 or 404) { name = product.CategoryId.ToString(); }
            return View(new ProductDetailPage(product, name));
        }
        catch (ApiCallException exception) { return Failure(exception, "/products"); }
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        try { var form = new ProductForm(); await PopulateCategories(form, ct); return View(form); }
        catch (ApiCallException exception) { return Failure(exception, "/products"); }
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create(ProductForm form, CancellationToken ct)
    {
        try
        {
            if (ModelState.IsValid)
            {
                var created = await api.CreateProductAsync(form.ToRequest(), ct);
                TempData["Success"] = "Producto creado con stock inicial de cero.";
                return await AfterWrite(created.Id);
            }
            await PopulateCategories(form, ct);
            return View(form);
        }
        catch (ApiCallException exception)
        {
            if (exception.StatusCode is 401 or 403 or 0 or >= 500) return Failure(exception, "/products/create");
            AddFormError(exception);
            try { await PopulateCategories(form, ct); }
            catch (ApiCallException selectionError) { return Failure(selectionError, "/products/create"); }
            return View(form);
        }
    }

    [HttpGet("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        ViewData["Id"] = id;
        try
        {
            ProductDto product;
            try { product = await api.GetProductAsync(id, ct); }
            catch (ApiCallException exception) when (exception.StatusCode == 403)
            {
                ViewData["WriteOnly"] = true;
                var empty = new ProductForm();
                await PopulateCategories(empty, ct);
                return View(empty);
            }
            var form = new ProductForm
            {
                Name = product.Name, Description = product.Description, Sku = product.Sku,
                PriceText = product.Price.ToString("0.00", CultureInfo.InvariantCulture), CategoryId = product.CategoryId
            };
            await PopulateCategories(form, ct);
            ViewData["Id"] = id;
            ViewData["Inactive"] = !product.IsActive;
            return View(form);
        }
        catch (ApiCallException exception) { return Failure(exception, "/products"); }
    }

    [HttpPost("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id, ProductForm form, CancellationToken ct)
    {
        ViewData["Id"] = id;
        try
        {
            if (ModelState.IsValid)
            {
                await api.UpdateProductAsync(id, form.ToRequest(), ct);
                TempData["Success"] = "Cambios guardados sin modificar el stock.";
                return await AfterWrite(id);
            }
            await PopulateCategories(form, ct);
            return View(form);
        }
        catch (ApiCallException exception)
        {
            if (exception.StatusCode is 401 or 403 or 0 or >= 500
                || (exception.StatusCode == 404 && exception.Code != "CATEGORY_NOT_FOUND"))
                return Failure(exception, "/products");
            AddFormError(exception);
            try { await PopulateCategories(form, ct); }
            catch (ApiCallException selectionError) { return Failure(selectionError, $"/products/{id}"); }
            return View(form);
        }
    }

    [HttpGet("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        try { return View(await api.GetProductAsync(id, ct)); }
        catch (ApiCallException exception) when (exception.StatusCode == 403)
        {
            return View("~/Views/Shared/DeactivateById.cshtml", new DeactivateById(id, $"/products/{id}/deactivate", "producto"));
        }
        catch (ApiCallException exception) { return Failure(exception, "/products"); }
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateConfirmed(Guid id, CancellationToken ct)
    {
        try
        {
            await api.DeleteProductAsync(id, ct);
            TempData["Success"] = "Producto desactivado; stock e historial conservados.";
            return await AfterWrite(id);
        }
        catch (ApiCallException exception) { return Failure(exception, $"/products/{id}"); }
    }

    private async Task PopulateCategories(ProductForm form, CancellationToken ct)
    {
        List<CategoryDto> categories;
        try { categories = (await api.GetActiveCategoriesAsync(ct)).ToList(); }
        catch (ApiCallException exception) when (exception.StatusCode == 403)
        {
            ViewData["ManualCategory"] = true;
            form.Categories = [];
            return;
        }
        if (form.CategoryId.HasValue && categories.All(category => category.Id != form.CategoryId.Value))
        {
            try
            {
                var current = await api.GetCategoryAsync(form.CategoryId.Value, ct);
                categories.Add(current);
                ViewData["CategoryWarning"] = "La categoría actual está inactiva. Elige otra categoría activa para guardar.";
            }
            catch (ApiCallException exception) when (exception.StatusCode == 404)
            {
                ViewData["CategoryWarning"] = "La categoría actual ya no existe. Elige una categoría activa.";
            }
        }
        form.Categories = categories;
    }

    private async Task<IActionResult> AfterWrite(Guid id)
    {
        if (await permissions.HasAsync("products.read")) return SafeReturnUrl() is string target ? Redirect(target) : RedirectToAction(nameof(Details), new { id });
        TempData["Success"] += $" Identificador: {id}.";
        return Redirect("/products");
    }
}




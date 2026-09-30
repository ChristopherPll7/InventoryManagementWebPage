using InventoryManagement.Web.Models;
using InventoryManagement.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Web.Controllers;

[Authorize]
[Route("categories")]
public sealed class CategoriesController(InventoryApiClient api, PermissionService permissions) : UiController
{
    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        if (InvalidPagination(page, pageSize)) ModelState.AddModelError("", "La página debe estar entre 1 y 1000 y el tamaño entre 1 y 100.");
        if (!ModelState.IsValid) return QueryFailure(new HistoryFilter { Page = page, PageSize = pageSize });
        if (!await permissions.HasAsync("categories.read"))
            return View("~/Views/Shared/DirectAccess.cshtml", new DirectAccessForm { Category = true });
        try { return View(await api.GetCategoriesAsync(page, pageSize, ct)); }
        catch (ApiCallException exception) when (exception.StatusCode == 400)
        {
            ModelState.AddModelError("", "La API rechazó la paginación. Revisa la página y el tamaño.");
            return QueryFailure(new HistoryFilter { Page = page, PageSize = pageSize });
        }
        catch (ApiCallException exception) { return Failure(exception, "/categories"); }
    }

    [HttpGet("open")]
    public Task<IActionResult> Open(DirectAccessForm form) => OpenById(form, true, permissions);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        try { return View(await api.GetCategoryAsync(id, ct)); }
        catch (ApiCallException exception) { return Failure(exception, "/categories"); }
    }

    [HttpGet("create")]
    public IActionResult Create() => View(new CategoryForm());

    [HttpPost("create")]
    public async Task<IActionResult> Create(CategoryForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(form);
        try
        {
            var created = await api.CreateCategoryAsync(form.ToRequest(), ct);
            TempData["Success"] = "Categoría creada correctamente.";
            return await AfterWrite(created.Id);
        }
        catch (ApiCallException exception)
        {
            if (exception.StatusCode is 401 or 403 or 0 or >= 500) return Failure(exception, "/categories/create");
            AddFormError(exception);
            return View(form);
        }
    }

    [HttpGet("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        ViewData["Id"] = id;
        try
        {
            var category = await api.GetCategoryAsync(id, ct);
            ViewData["Id"] = id;
            return View(new CategoryForm { Name = category.Name, Description = category.Description });
        }
        catch (ApiCallException exception) when (exception.StatusCode == 403)
        {
            ViewData["WriteOnly"] = true;
            return View(new CategoryForm());
        }
        catch (ApiCallException exception) { return Failure(exception, "/categories"); }
    }

    [HttpPost("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id, CategoryForm form, CancellationToken ct)
    {
        ViewData["Id"] = id;
        if (!ModelState.IsValid) return View(form);
        try
        {
            await api.UpdateCategoryAsync(id, form.ToRequest(), ct);
            TempData["Success"] = "Cambios guardados.";
            return await AfterWrite(id);
        }
        catch (ApiCallException exception)
        {
            if (exception.StatusCode is 401 or 403 or 404 or 0 or >= 500) return Failure(exception, $"/categories/{id}");
            AddFormError(exception);
            return View(form);
        }
    }

    [HttpGet("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        try { return View(await api.GetCategoryAsync(id, ct)); }
        catch (ApiCallException exception) when (exception.StatusCode == 403)
        {
            return View("~/Views/Shared/DeactivateById.cshtml", new DeactivateById(id, $"/categories/{id}/deactivate", "categoría"));
        }
        catch (ApiCallException exception) { return Failure(exception, "/categories"); }
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateConfirmed(Guid id, CancellationToken ct)
    {
        try
        {
            await api.DeleteCategoryAsync(id, ct);
            TempData["Success"] = "Categoría desactivada. Permanece disponible para consulta.";
            return await AfterWrite(id);
        }
        catch (ApiCallException exception) { return Failure(exception, $"/categories/{id}"); }
    }

    private async Task<IActionResult> AfterWrite(Guid id)
    {
        if (await permissions.HasAsync("categories.read")) return SafeReturnUrl() is string target ? Redirect(target) : RedirectToAction(nameof(Details), new { id });
        TempData["Success"] += $" Identificador: {id}.";
        return Redirect("/categories");
    }
}




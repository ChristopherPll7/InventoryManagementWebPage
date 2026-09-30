using InventoryManagement.Web.Models;
using InventoryManagement.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Web.Controllers;

[Authorize]
[Route("inventory/products/{productId:guid}")]
public sealed class InventoryController(InventoryApiClient api, PermissionService permissions) : UiController
{
    [HttpGet("")]
    public async Task<IActionResult> Details(Guid productId, [FromQuery] HistoryFilter filter, CancellationToken ct)
    {
        var errors = filter.Validate();
        foreach (var error in errors) ModelState.AddModelError("", error);
        if (!ModelState.IsValid) return QueryFailure(filter, inventory: true);

        try
        {
            return View(await BuildPage(productId, filter, new MovementForm(), ct));
        }
        catch (ApiCallException exception) when (exception.StatusCode == 400)
        {
            ModelState.AddModelError("", "La API rechazó los filtros. Revisa el tipo, las fechas y la paginación.");
            return QueryFailure(filter, inventory: true);
        }
        catch (ApiCallException exception) { return Failure(exception, $"/products/{productId}"); }
    }

    [HttpPost("movements")]
    public async Task<IActionResult> Register(Guid productId, MovementForm form, CancellationToken ct, [FromQuery(Name = "history")] HistoryFilter? history = null)
    {
        history ??= new HistoryFilter();
        var filterErrors = history.Validate();
        var queryBindingFailed = ModelState.Any(entry => entry.Value?.Errors.Count > 0
            && entry.Key.Split('.').Last() is "Page" or "PageSize" or "StartDateText" or "EndDateText");
        if (filterErrors.Count > 0 || queryBindingFailed)
        {
            foreach (var error in filterErrors) ModelState.AddModelError("", error);
            ViewData["ProductId"] = productId;
            return await MovementView(productId, form, ct);
        }
        if (productId == Guid.Empty)
        {
            ViewData["ProductId"] = productId;
            ModelState.AddModelError("", "Indica un producto con UUID distinto de cero desde Operaciones.");
            return await MovementView(productId, form, ct);
        }
        if (!ModelState.IsValid) return await RenderForm(productId, form, history, ct);
        try
        {
            var result = await api.RegisterMovementAsync(form.ToRequest(productId), ct);
            TempData["Success"] = $"Movimiento registrado. Existencia actual: {result.CurrentStock:N0}.";
            return RedirectToAction(await permissions.HasAsync("inventory.read") ? nameof(Details) : nameof(Movement),
                new { productId, history.Type, history.StartDateText, history.EndDateText, history.Page, history.PageSize, returnUrl = SafeReturnUrl() });
        }
        catch (ApiCallException exception)
        {
            if (exception.StatusCode is 401 or 403 or 404 or 0 or >= 500) return Failure(exception, $"/inventory/products/{productId}");
            AddFormError(exception);
            if (exception.Code == "PRODUCT_INACTIVE") ViewData["Inactive"] = true;
            return await RenderForm(productId, form, history, ct);
        }
    }

    private async Task<IActionResult> RenderForm(Guid productId, MovementForm form, HistoryFilter filter, CancellationToken ct)
    {
        ViewData["ProductId"] = productId;
        if (!await permissions.HasAsync("inventory.read")) return await MovementView(productId, form, ct);
        try
        {
            var page = await BuildPage(productId, filter, form, ct);
            return View("Details", ViewData["Inactive"] is true ? page with { IsActive = false } : page);
        }
        catch (ApiCallException exception) when (exception.StatusCode == 403) { return await MovementView(productId, form, ct); }
        catch (ApiCallException exception) { return Failure(exception, $"/products/{productId}"); }
    }

    private async Task<InventoryPage> BuildPage(Guid productId, HistoryFilter filter, MovementForm form, CancellationToken ct)
    {
        var balance = await api.GetInventoryAsync(productId, ct);
        var history = await api.GetMovementsAsync(productId, filter, ct);
        bool? isActive = null;
        if (await permissions.HasAsync("products.read"))
        {
            try { isActive = (await api.GetProductAsync(productId, ct)).IsActive; }
            catch (ApiCallException exception) when (exception.StatusCode == 403) { }
        }
        return new InventoryPage(balance, history, filter, form, isActive);
    }

    [HttpGet("movements")]
    public Task<IActionResult> Movement(Guid productId, CancellationToken ct = default) =>
        MovementView(productId, new MovementForm(), ct);

    private async Task<IActionResult> MovementView(Guid productId, MovementForm form, CancellationToken ct)
    {
        ViewData["ProductId"] = productId;
        ViewData.Remove("ProductName");
        if (productId != Guid.Empty)
        {
            try
            {
                if (await permissions.HasAsync("products.read"))
                    ViewData["ProductName"] = (await api.GetProductAsync(productId, ct)).Name;
                else if (await permissions.HasAsync("inventory.read"))
                    ViewData["ProductName"] = (await api.GetInventoryAsync(productId, ct)).Name;
            }
            catch (ApiCallException)
            {
                // Optional display metadata must not block an independently authorized write.
            }
        }
        return View("Movement", form);
    }
}

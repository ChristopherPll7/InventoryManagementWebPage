using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Web.Controllers;

public sealed class HomeController : Controller
{
    [AllowAnonymous]
    [HttpGet("/")]
    public IActionResult Index() => View();

    [AllowAnonymous]
    [HttpGet("/login")]
    public IActionResult Login(string? returnUrl = null)
    {
        var target = Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Action("Index", "Home")!;
        return Challenge(new AuthenticationProperties { RedirectUri = target }, OpenIdConnectDefaults.AuthenticationScheme);
    }

    [Authorize]
    [HttpPost("/logout")]
    [ValidateAntiForgeryToken]
    public IActionResult Logout() => SignOut(new AuthenticationProperties { RedirectUri = "/" },
        CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme);

    [AllowAnonymous]
    [HttpGet("/access-denied")]
    public IActionResult AccessDenied() => View();

    [AllowAnonymous]
    [HttpGet("/error")]
    public IActionResult Error() => View("~/Views/Shared/ApiError.cshtml",
        new ErrorPage("Ocurrió un error inesperado.", "INTERNAL_ERROR", "/"));
}

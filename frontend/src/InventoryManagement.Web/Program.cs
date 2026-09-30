using InventoryManagement.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(builder.Configuration["ASPNETCORE_URLS"] ?? "http://localhost:5090");

var apiBaseUrl = builder.Configuration["Api:BaseUrl"] ?? throw new InvalidOperationException("Api:BaseUrl is required.");
var authority = builder.Configuration["Authentication:Authority"] ?? throw new InvalidOperationException("Authentication:Authority is required.");
var clientId = builder.Configuration["Authentication:ClientId"] ?? throw new InvalidOperationException("Authentication:ClientId is required.");

builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddHttpClient<TokenSessionService>();
builder.Services.AddHttpClient<InventoryApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(20);
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.Cookie.Name = "InventoryManagement.Web.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;
    options.AccessDeniedPath = "/access-denied";
})
.AddOpenIdConnect(options =>
{
    options.Authority = authority;
    options.ClientId = clientId;
    options.ClientSecret = null;
    options.ResponseType = "code";
    options.ResponseMode = "query";
    options.CorrelationCookie.SameSite = SameSiteMode.Lax;
    options.NonceCookie.SameSite = SameSiteMode.Lax;
    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.NonceCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.UsePkce = true;
    options.RequireHttpsMetadata = !authority.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
    options.SaveTokens = true;
    options.GetClaimsFromUserInfoEndpoint = false;
    options.CallbackPath = "/signin-oidc";
    options.SignedOutCallbackPath = "/signout-callback-oidc";
    options.Scope.Clear();
    options.Scope.Add("openid");
});

builder.Services.AddAuthorization();
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
}
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();

using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace InventoryManagement.Web.Services;

public sealed class TokenSessionService(HttpClient http, IHttpContextAccessor accessor, IConfiguration configuration,
    ILogger<TokenSessionService> logger)
{
    private string? accessToken;

    public async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        if (accessToken is not null) return accessToken;
        var context = accessor.HttpContext ?? throw new InvalidOperationException("An HTTP context is required.");
        var ticket = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var properties = ticket.Properties ?? throw SessionExpired();
        var token = properties.GetTokenValue("access_token");
        var expires = properties.GetTokenValue("expires_at");
        if (string.IsNullOrWhiteSpace(token)) throw SessionExpired();
        if (DateTimeOffset.TryParse(expires, CultureInfo.InvariantCulture, DateTimeStyles.None, out var expiry)
            && expiry > DateTimeOffset.UtcNow.AddSeconds(30)) return accessToken = token;

        var refreshToken = properties.GetTokenValue("refresh_token");
        if (string.IsNullOrWhiteSpace(refreshToken) || ticket.Principal is null) throw SessionExpired();
        var authority = configuration["Authentication:Authority"]?.TrimEnd('/') ?? throw SessionExpired();
        var clientId = configuration["Authentication:ClientId"] ?? throw SessionExpired();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{authority}/protocol/openid-connect/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token", ["client_id"] = clientId, ["refresh_token"] = refreshToken
                })
            };
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) throw SessionExpired();
            using var json = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct) ?? throw SessionExpired();
            var root = json.RootElement;
            var renewed = root.GetProperty("access_token").GetString();
            var seconds = root.GetProperty("expires_in").GetInt32();
            if (string.IsNullOrWhiteSpace(renewed) || seconds <= 0) throw SessionExpired();

            properties.UpdateTokenValue("access_token", renewed);
            properties.UpdateTokenValue("expires_at", DateTimeOffset.UtcNow.AddSeconds(seconds).ToString("O", CultureInfo.InvariantCulture));
            if (root.TryGetProperty("refresh_token", out var rotated) && !string.IsNullOrWhiteSpace(rotated.GetString()))
                properties.UpdateTokenValue("refresh_token", rotated.GetString()!);
            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, ticket.Principal, properties);
            return accessToken = renewed;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            logger.LogWarning("The user session could not be renewed with Keycloak.");
            throw SessionExpired();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw SessionExpired();
        }
    }

    private static ApiCallException SessionExpired() =>
        new(401, "UNAUTHORIZED", "Tu sesión ha expirado. Inicia sesión de nuevo.");
}

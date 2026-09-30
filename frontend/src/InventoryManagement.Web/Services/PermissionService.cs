using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;

namespace InventoryManagement.Web.Services;

public sealed class PermissionService(IHttpContextAccessor accessor)
{
    public async Task<bool> HasAsync(string permission)
    {
        var context = accessor.HttpContext;
        if (context?.User.Identity?.IsAuthenticated != true) return false;
        var token = await context.GetTokenAsync("access_token");
        if (string.IsNullOrWhiteSpace(token)) return false;

        try
        {
            var payload = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight((payload.Length + 3) / 4 * 4, '=');
            using var json = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            if (!json.RootElement.TryGetProperty("permissions", out var permissions)) return false;
            return permissions.ValueKind switch
            {
                JsonValueKind.Array => permissions.EnumerateArray().Any(item => item.GetString() == permission),
                JsonValueKind.String => permissions.GetString() == permission,
                _ => false
            };
        }
        catch (Exception exception) when (exception is FormatException or IndexOutOfRangeException or JsonException)
        {
            return false;
        }
    }
}

using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace Azure.Container.Dashboard.Auth;

internal sealed class ClientPrincipal
{
    [JsonPropertyName("auth_typ")]
    public string? IdentityProvider { get; set; }

    [JsonPropertyName("name_typ")]
    public string? NameClaimType { get; set; }

    [JsonPropertyName("role_typ")]
    public string? RoleClaimType { get; set; }

    [JsonPropertyName("claims")]
    public List<ClientPrincipalClaim> Claims { get; set; } = [];
}

internal sealed class ClientPrincipalClaim
{
    [JsonPropertyName("typ")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("val")]
    public string Value { get; set; } = string.Empty;
}

internal static class ClientPrincipalParser
{
    private const string HeaderName = "X-MS-CLIENT-PRINCIPAL";

    public static ClaimsPrincipal? Parse(HttpRequest request)
    {
        if (!request.Headers.TryGetValue(HeaderName, out var headerValues))
            return null;

        var headerValue = headerValues.ToString();
        if (string.IsNullOrWhiteSpace(headerValue))
            return null;

        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(headerValue);
        }
        catch (FormatException)
        {
            return null;
        }

        ClientPrincipal? principal;
        try
        {
            principal = JsonSerializer.Deserialize<ClientPrincipal>(decoded);
        }
        catch (JsonException)
        {
            return null;
        }

        if (principal is null)
            return null;

        var identity = new ClaimsIdentity(
            principal.Claims.Select(c => new Claim(c.Type, c.Value)),
            principal.IdentityProvider,
            principal.NameClaimType,
            principal.RoleClaimType);

        return new ClaimsPrincipal(identity);
    }

    public static string? GetUserName(ClaimsPrincipal principal)
        => principal.Identity?.Name;

    public static string? GetUserEmail(ClaimsPrincipal principal)
        => principal.FindFirst(ClaimTypes.Email)?.Value
           ?? principal.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value;

    public static string[] GetUserRoles(ClaimsPrincipal principal)
        => principal.Claims
            .Where(c => c.Type == ClaimTypes.Role
                        || c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
                        || c.Type == "roles")
            .Select(c => c.Value)
            .ToArray();

    public static bool HasRole(ClaimsPrincipal principal, string role)
        => principal.IsInRole(role)
           || GetUserRoles(principal).Contains(role, StringComparer.OrdinalIgnoreCase);
}

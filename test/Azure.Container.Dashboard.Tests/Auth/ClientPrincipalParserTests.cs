using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Azure.Container.Dashboard.Auth;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Azure.Container.Dashboard.Tests.Auth;

public class ClientPrincipalParserTests
{
    private static HttpRequest CreateRequest(string? headerValue = null)
    {
        var context = new DefaultHttpContext();
        if (headerValue is not null)
            context.Request.Headers["X-MS-CLIENT-PRINCIPAL"] = headerValue;
        return context.Request;
    }

    private static string EncodeClientPrincipal(object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    [Fact]
    public void Parse_MissingHeader_ReturnsNull()
    {
        var request = CreateRequest();
        var result = ClientPrincipalParser.Parse(request);
        Assert.Null(result);
    }

    [Fact]
    public void Parse_EmptyHeader_ReturnsNull()
    {
        var request = CreateRequest("");
        var result = ClientPrincipalParser.Parse(request);
        Assert.Null(result);
    }

    [Fact]
    public void Parse_InvalidBase64_ReturnsNull()
    {
        var request = CreateRequest("not-valid-base64!!!");
        var result = ClientPrincipalParser.Parse(request);
        Assert.Null(result);
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsNull()
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("this is not json"));
        var request = CreateRequest(encoded);
        var result = ClientPrincipalParser.Parse(request);
        Assert.Null(result);
    }

    [Fact]
    public void Parse_ValidHeader_ReturnsClaimsPrincipalWithCorrectIdentity()
    {
        var payload = new
        {
            auth_typ = "aad",
            name_typ = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress",
            role_typ = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
            claims = new[]
            {
                new { typ = "name", val = "John Doe" },
                new { typ = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", val = "john@contoso.com" },
                new { typ = "roles", val = "Dashboard.Admin" }
            }
        };
        var request = CreateRequest(EncodeClientPrincipal(payload));

        var result = ClientPrincipalParser.Parse(request);

        Assert.NotNull(result);
        Assert.True(result.Identity!.IsAuthenticated);
        Assert.Equal("aad", result.Identity.AuthenticationType);
    }

    [Fact]
    public void Parse_EmptyClaimsArray_ReturnsPrincipalWithEmptyClaims()
    {
        var payload = new
        {
            auth_typ = "aad",
            name_typ = "name",
            role_typ = "roles",
            claims = Array.Empty<object>()
        };
        var request = CreateRequest(EncodeClientPrincipal(payload));

        var result = ClientPrincipalParser.Parse(request);

        Assert.NotNull(result);
        Assert.True(result.Identity!.IsAuthenticated);
        Assert.Empty(result.Claims);
    }

    [Fact]
    public void GetUserName_ReturnsName()
    {
        var payload = new
        {
            auth_typ = "aad",
            name_typ = "name",
            role_typ = "roles",
            claims = new[]
            {
                new { typ = "name", val = "John Doe" }
            }
        };
        var request = CreateRequest(EncodeClientPrincipal(payload));
        var principal = ClientPrincipalParser.Parse(request)!;

        var name = ClientPrincipalParser.GetUserName(principal);

        Assert.Equal("John Doe", name);
    }

    [Fact]
    public void GetUserEmail_ReturnsEmail()
    {
        var payload = new
        {
            auth_typ = "aad",
            name_typ = "name",
            role_typ = "roles",
            claims = new[]
            {
                new { typ = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", val = "john@contoso.com" }
            }
        };
        var request = CreateRequest(EncodeClientPrincipal(payload));
        var principal = ClientPrincipalParser.Parse(request)!;

        var email = ClientPrincipalParser.GetUserEmail(principal);

        Assert.Equal("john@contoso.com", email);
    }

    [Fact]
    public void GetUserRoles_ReturnsAllRoles()
    {
        var payload = new
        {
            auth_typ = "aad",
            name_typ = "name",
            role_typ = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
            claims = new[]
            {
                new { typ = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role", val = "Dashboard.Admin" },
                new { typ = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role", val = "Dashboard.Reader" }
            }
        };
        var request = CreateRequest(EncodeClientPrincipal(payload));
        var principal = ClientPrincipalParser.Parse(request)!;

        var roles = ClientPrincipalParser.GetUserRoles(principal);

        Assert.Equal(2, roles.Length);
        Assert.Contains("Dashboard.Admin", roles);
        Assert.Contains("Dashboard.Reader", roles);
    }

    [Fact]
    public void HasRole_ReturnsTrueForMatchingRole()
    {
        var payload = new
        {
            auth_typ = "aad",
            name_typ = "name",
            role_typ = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
            claims = new[]
            {
                new { typ = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role", val = "Dashboard.Admin" }
            }
        };
        var request = CreateRequest(EncodeClientPrincipal(payload));
        var principal = ClientPrincipalParser.Parse(request)!;

        Assert.True(ClientPrincipalParser.HasRole(principal, "Dashboard.Admin"));
    }

    [Fact]
    public void HasRole_ReturnsFalseForNonMatchingRole()
    {
        var payload = new
        {
            auth_typ = "aad",
            name_typ = "name",
            role_typ = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
            claims = new[]
            {
                new { typ = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role", val = "Dashboard.Reader" }
            }
        };
        var request = CreateRequest(EncodeClientPrincipal(payload));
        var principal = ClientPrincipalParser.Parse(request)!;

        Assert.False(ClientPrincipalParser.HasRole(principal, "Dashboard.Admin"));
    }
}

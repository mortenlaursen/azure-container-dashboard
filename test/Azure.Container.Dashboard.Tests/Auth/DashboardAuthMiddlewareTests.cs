using System.Text;
using System.Text.Json;
using Azure.Container.Dashboard.Auth;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Azure.Container.Dashboard.Tests.Auth;

public class DashboardAuthMiddlewareTests
{
    private static string EncodeClientPrincipal(object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    private static string CreateValidHeader(params string[] roles)
    {
        var claims = new List<object>
        {
            new { typ = "name", val = "Test User" },
            new { typ = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", val = "test@contoso.com" }
        };
        foreach (var role in roles)
            claims.Add(new { typ = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role", val = role });

        return EncodeClientPrincipal(new
        {
            auth_typ = "aad",
            name_typ = "name",
            role_typ = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
            claims
        });
    }

    private static (DashboardAuthMiddleware middleware, DefaultHttpContext context) CreateMiddleware(
        DashboardOptions options, string path, bool addHeader = false, params string[] roles)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        if (addHeader)
            context.Request.Headers["X-MS-CLIENT-PRINCIPAL"] = CreateValidHeader(roles);

        // Store nextCalled flag in context items for assertion
        context.Items["NextCalled"] = false;
        var actualNext = new RequestDelegate(ctx =>
        {
            ctx.Items["NextCalled"] = true;
            return Task.CompletedTask;
        });
        var middleware = new DashboardAuthMiddleware(actualNext, options);

        return (middleware, context);
    }

    [Fact]
    public async Task NonDashboardRoute_PassesThrough()
    {
        var options = new DashboardOptions { RequireAuthentication = true };
        var (middleware, context) = CreateMiddleware(options, "/api/other");

        await middleware.InvokeAsync(context);

        Assert.True((bool)context.Items["NextCalled"]!);
    }

    [Fact]
    public async Task DashboardRoute_AuthDisabled_PassesThrough()
    {
        var options = new DashboardOptions { RequireAuthentication = false };
        var (middleware, context) = CreateMiddleware(options, "/dashboard");

        await middleware.InvokeAsync(context);

        Assert.True((bool)context.Items["NextCalled"]!);
    }

    [Fact]
    public async Task DashboardRoute_AuthEnabled_NoHeader_Returns401()
    {
        var options = new DashboardOptions { RequireAuthentication = true };
        var (middleware, context) = CreateMiddleware(options, "/dashboard");

        await middleware.InvokeAsync(context);

        Assert.Equal(401, context.Response.StatusCode);
        Assert.False((bool)context.Items["NextCalled"]!);
    }

    [Fact]
    public async Task DashboardRoute_AuthEnabled_NoHeader_Returns401WithJsonBody()
    {
        var options = new DashboardOptions { RequireAuthentication = true };
        var (middleware, context) = CreateMiddleware(options, "/dashboard");
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(401, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task DashboardRoute_AuthEnabled_ValidHeader_EmptyAllowedRoles_PassesThrough()
    {
        var options = new DashboardOptions { RequireAuthentication = true, AllowedRoles = [] };
        var (middleware, context) = CreateMiddleware(options, "/dashboard", addHeader: true);

        await middleware.InvokeAsync(context);

        Assert.True((bool)context.Items["NextCalled"]!);
    }

    [Fact]
    public async Task DashboardRoute_AuthEnabled_ValidHeader_MatchingRole_PassesThrough()
    {
        var options = new DashboardOptions
        {
            RequireAuthentication = true,
            AllowedRoles = ["Dashboard.Admin"]
        };
        var (middleware, context) = CreateMiddleware(options, "/dashboard", addHeader: true, "Dashboard.Admin");

        await middleware.InvokeAsync(context);

        Assert.True((bool)context.Items["NextCalled"]!);
    }

    [Fact]
    public async Task DashboardRoute_AuthEnabled_ValidHeader_NonMatchingRole_Returns403()
    {
        var options = new DashboardOptions
        {
            RequireAuthentication = true,
            AllowedRoles = ["Dashboard.Admin"]
        };
        var (middleware, context) = CreateMiddleware(options, "/dashboard", addHeader: true, "SomeOtherRole");
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(403, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task DashboardSubRoute_AuthEnabled_NoHeader_Returns401()
    {
        var options = new DashboardOptions { RequireAuthentication = true };
        var (middleware, context) = CreateMiddleware(options, "/dashboard/functions");

        await middleware.InvokeAsync(context);

        Assert.Equal(401, context.Response.StatusCode);
    }
}

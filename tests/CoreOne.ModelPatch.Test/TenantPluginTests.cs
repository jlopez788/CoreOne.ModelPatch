using CoreOne.Identity.Contracts;
using CoreOne.ModelPatch.Extensions;
using CoreOne.ModelPatch.Tenants;
using CoreOne.ModelPatch.Tenants.Services;
using CoreOne.ModelPatch.Test.Data;
using CoreOne.ModelPatch.Test.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace CoreOne.ModelPatch.Test;

/// <summary>
/// Tests for the tenant plugin system
/// </summary>
[TestClass]
public class TenantPluginTests
{
    protected SToken Token = SToken.Create();

    #region Tenant Provider Tests

    [TestMethod]
    public async Task HttpContextTenantProvider_ReadsTenantFromClaim()
    {
        var context = new DefaultHttpContext {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("tenant", "claim-tenant")], "test"))
        };
        var accessor = new HttpContextAccessor { HttpContext = context };
        var options = new TenantPluginOptions {
            TenatAccessor = new UserClaimTenantAccessor("tenant")
        };
        var provider = new HttpContextTenantProvider(accessor, options);

        var tenant = await provider.GetTenantKey();

        Assert.AreEqual("claim-tenant", tenant);
    }

    [TestMethod]
    public async Task HttpContextTenantProvider_ReadsTenantFromHeader()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Tenant-Id"] = "header-tenant";
        var accessor = new HttpContextAccessor { HttpContext = context };
        var provider = new HttpContextTenantProvider(accessor, new TenantPluginOptions {
            TenatAccessor = new AuthorizationHeaderTenantAccessor("X-Tenant-Id")
        });

        var tenant = await provider.GetTenantKey();

        Assert.AreEqual("header-tenant", tenant);
    }

    [TestMethod]
    public async Task HttpContextTenantProvider_ReadsTenantFromRoute()
    {
        var context = new DefaultHttpContext();
        var routeData = new RouteData();
        routeData.Values["tenantId"] = "route-tenant";
        context.Features.Set<IRoutingFeature>(new RoutingFeature { RouteData = routeData });

        var accessor = new HttpContextAccessor { HttpContext = context };
        var options = new TenantPluginOptions {
            TenatAccessor = new RouteParameterTenantAccessor("tenantId")
        };
        var provider = new HttpContextTenantProvider(accessor, options);

        var tenant = await provider.GetTenantKey();

        Assert.AreEqual("route-tenant", tenant);
    }

    [TestMethod]
    public async Task HttpContextTenantProvider_ReturnsNullWithoutContext()
    {
        var provider = new HttpContextTenantProvider(new HttpContextAccessor(), new TenantPluginOptions());

        var tenant = await provider.GetTenantKey();

        Assert.IsNull(tenant);
    }

    [TestMethod]
    public async Task MockTenantProvider_GetTenantIdAsync_ReturnsTenantId()
    {
        var provider = new MockTenantProvider();
        provider.SetCurrentTenant("tenant-456");

        var tenantId = await provider.GetTenantKey();
        Assert.AreEqual("tenant-456", tenantId);
    }

    [TestMethod]
    public void MockTenantProvider_NoTenantContext_ReturnsFalse()
    {
        var provider = new MockTenantProvider();

        Assert.IsFalse(provider.TryGetTenantId(out _));
    }

    [TestMethod]
    public void MockTenantProvider_SetAndGetTenantId()
    {
        var provider = new MockTenantProvider();
        provider.SetCurrentTenant("tenant-123");

        Assert.IsTrue(provider.TryGetTenantId(out var tenantId));
        Assert.AreEqual("tenant-123", tenantId);
    }

    #endregion

    #region Tenant Plugin Options Tests

    [TestMethod]
    public void AddTenantSupport_CustomProviderOverload_RegistersConfiguredProviderOptions()
    {
        var root = new ServiceCollection()
            .AddLogging()
            .AddTenantSupport<MockTenantProvider>(provider => {
                provider.TenatAccessor = new AuthorizationHeaderTenantAccessor("X-Custom-Tenant");
            })
            .BuildServiceProvider();

        var options = root.GetRequiredService<TenantPluginOptions>();
        Assert.AreEqual("X-Custom-Tenant", options.TenatAccessor?.ToString());
    }

    [TestMethod]
    public void AddTenantSupport_DefaultProvider_RegistersHttpContextDependencies()
    {
        var root = new ServiceCollection()
            .AddLogging()
            .AddTenantSupport(p => p.TenatAccessor = new AuthorizationHeaderTenantAccessor("X-Tenant-Id"))
            .BuildServiceProvider();

        using var scope = root.CreateScope();
        var tenantProvider = scope.ServiceProvider.GetRequiredService<ITenantProvider>();
        var httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var providerOptions = scope.ServiceProvider.GetRequiredService<TenantPluginOptions>();

        Assert.IsInstanceOfType<HttpContextTenantProvider>(tenantProvider);
        Assert.IsNotNull(httpContextAccessor);
        Assert.AreEqual("X-Tenant-Id", providerOptions.TenatAccessor?.ToString());
    }

    [TestMethod]
    public void TenantPluginOptions_CanConfigureMultiTenantTypes()
    {
        var options = new TenantPluginOptions {
            MultiTenantEntityTypes = [typeof(Blog)]
        };

        Assert.Contains(typeof(Blog), options.MultiTenantEntityTypes);
    }

    [TestMethod]
    public void TenantPluginOptions_DefaultValues()
    {
        var options = new TenantPluginOptions();

        Assert.IsTrue(options.ThrowOnTenantMismatch);
        Assert.IsEmpty(options.MultiTenantEntityTypes);
    }

    #endregion

    #region Tenant Plugin Integration Tests

    [TestMethod]
    public async Task Patch_NewTenantOwnedEntity_AutoAssignsTenantKey()
    {
        using var scope = CreateScope("tenant-a");
        var service = scope.ServiceProvider.GetRequiredService<IDataModelService<TestDbContext>>();
        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var blog = new Blog { Name = "Tenant Blog" };

        var result = await service.Patch(blog, Token);

        Assert.AreEqual(ResultType.Success, result.ResultType);
        var saved = await db.Blogs.FirstOrDefaultAsync(Token);
        Assert.IsNotNull(saved);
        Assert.AreEqual("tenant-a", saved.TenantId);
    }

    [TestMethod]
    public async Task Patch_NewTenantOwnedEntity_WhenAutoInjectDisabledAndNoTenantField_Fails()
    {
        using var scope = CreateScope(null);
        var service = scope.ServiceProvider.GetRequiredService<IDataModelService<TestDbContext>>();

        var result = await service.Patch(new Blog { Name = "No Auto Inject" }, Token);

        Assert.AreEqual(ResultType.Fail, result.ResultType);
        StringAssert.Contains(result.Message ?? string.Empty, "Tenant key is required");
    }

    [TestMethod]
    public async Task Patch_NewTenantOwnedEntity_WithMatchingProvidedTenantKey_Succeeds()
    {
        using var scope = CreateScope("tenant-a");
        var service = scope.ServiceProvider.GetRequiredService<IDataModelService<TestDbContext>>();

        var result = await service.Patch(new Blog {
            Name = "Provided Tenant",
            TenantId = "TENANT-A"
        }, Token);

        Assert.AreEqual(ResultType.Success, result.ResultType);
    }

    [TestMethod]
    public async Task Patch_NewTenantOwnedEntity_WithMismatchedTenantKey_Fails()
    {
        using var scope = CreateScope("tenant-a");
        var service = scope.ServiceProvider.GetRequiredService<IDataModelService<TestDbContext>>();
        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var blog = new Blog { Name = "Tenant Blog", TenantId = "tenant-b" };

        var result = await service.Patch(blog, Token);

        Assert.AreEqual(ResultType.Fail, result.ResultType);
        StringAssert.Contains(result.Message ?? string.Empty, "Tenant key mismatch");
        Assert.AreEqual(0, await db.Blogs.CountAsync(Token));
    }

    [TestMethod]
    public async Task Patch_NonTenantOwnedEntity_SucceedsWithoutTenant()
    {
        using var scope = CreateScope(null);
        var service = scope.ServiceProvider.GetRequiredService<IDataModelService<TestDbContext>>();

        var result = await service.Patch(new Tag("tag-no-tenant"), Token);

        Assert.AreEqual(ResultType.Success, result.ResultType);
    }

    [TestMethod]
    public async Task Patch_TenantOwnedEntity_WithoutResolvedTenant_Fails()
    {
        using var scope = CreateScope(null);
        var service = scope.ServiceProvider.GetRequiredService<IDataModelService<TestDbContext>>();
        var blog = new Blog { Name = "Tenant Blog" };

        var result = await service.Patch(blog, Token);

        Assert.AreEqual(ResultType.Fail, result.ResultType);
        Assert.AreEqual("Tenant key is required", result.Message);
    }

    [TestMethod]
    public async Task Patch_UpdateTenantOwnedEntity_WhenDbTenantMismatchesContext_Fails()
    {
        using var scope = CreateScope("tenant-a");
        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IDataModelService<TestDbContext>>();
        var blogId = Guid.NewGuid();

        db.Blogs.Add(new Blog {
            BlogId = blogId,
            Name = "Original",
            TenantId = "tenant-b"
        });
        await db.SaveChangesAsync(Token);

        var result = await service.Patch(new Blog {
            BlogId = blogId,
            Name = "Updated"
        }, Token);

        Assert.AreEqual(ResultType.Fail, result.ResultType);
        StringAssert.Contains(result.Message ?? string.Empty, "does not belong to tenant");
    }

    [TestMethod]
    public async Task Patch_UpdateTenantOwnedEntity_WhenThrowOnTenantMismatchDisabled_Succeeds()
    {
        using var scope = CreateScope("tenant-a", options => options.ThrowOnTenantMismatch = false);
        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IDataModelService<TestDbContext>>();
        var blogId = Guid.NewGuid();

        db.Blogs.Add(new Blog {
            BlogId = blogId,
            Name = "Original",
            TenantId = "tenant-b"
        });
        await db.SaveChangesAsync(Token);

        var result = await service.Patch(new Blog {
            BlogId = blogId,
            Name = "Updated"
        }, Token);

        Assert.AreEqual(ResultType.Success, result.ResultType);
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// Mock ITenantProvider for testing
    /// </summary>
    public class MockTenantProvider : ITenantProvider
    {
        public string? CurrentTenant { get; set; }

        public ValueTask<object?> GetTenantKey() => ValueTask.FromResult((TryGetTenantId(out var p) ? p : null));

        public void SetCurrentTenant(string? tenantId) => CurrentTenant = tenantId;

        public bool TryGetTenantId(out object tenantId)
        {
            if (CurrentTenant is not null)
            {
                tenantId = CurrentTenant;
                return true;
            }
            tenantId = null!;
            return false;
        }
    }

    private static TestDbContext CreateContext()
    {
        var contextOptions = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase($"TenantPluginTests-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var context = new TestDbContext(contextOptions);
        context.Database.EnsureCreated();
        return context;
    }

    private IServiceScope CreateScope(string? tenantId, Action<TenantPluginOptions>? configure = null)
    {
        var context = CreateContext();
        var root = new ServiceCollection()
            .AddLogging()
            .AddSingleton(context)
            .AddModelPatch(p => p.UseNewtonsoftJsonPropertyNames())
            .AddTenantSupport<MockTenantProvider>(configure)
            .BuildServiceProvider();

        var scope = root.CreateScope();
        var provider = (MockTenantProvider)scope.ServiceProvider.GetRequiredService<ITenantProvider>();
        provider.SetCurrentTenant(tenantId);
        return scope;
    }

    #endregion
}
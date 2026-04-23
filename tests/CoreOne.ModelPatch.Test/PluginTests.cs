using CoreOne.ModelPatch.Extensions;
using CoreOne.ModelPatch.Models;
using CoreOne.ModelPatch.Services;
using CoreOne.ModelPatch.Test.Data;
using CoreOne.ModelPatch.Test.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoreOne.ModelPatch.Test;

/// <summary>
/// Tests for the plugin system infrastructure
/// </summary>
[TestClass]
public class PluginTests : Disposable
{
    protected SToken Token = SToken.Create();

    #region Plugin Context Tests

    [TestMethod]
    public void ModelProcessContext_Constructor_SetsProperties()
    {
        var entityType = typeof(Blog);
        var delta = new Delta { ["Name"] = "Updated" };
        var blog = new Blog { BlogId = ID.Create().AsGuid(), Name = "Original" };

        var context = new ModelProcessContext(new ModelContext(entityType), delta, blog, CrudType.Created);

        Assert.AreEqual(entityType, context.Type);
        Assert.AreEqual(delta, context.Delta);
        Assert.AreEqual(blog, context.Model);
        Assert.IsNotNull(context.AdditionalProperties);
    }

    [TestMethod]
    public void ModelProcessContext_Metadata_CanStoreAndRetrieveValues()
    {
        var context = new ModelProcessContext(typeof(Blog), new Delta(), new Blog(), CrudType.Created);

        context.Delta["TenantId"] = "tenant-123";
        context.Delta["UserId"] = "user-456";

        Assert.AreEqual("tenant-123", context.Delta["TenantId"]);
        Assert.AreEqual("user-456", context.Delta["UserId"]);
    }

    #endregion

    #region Backward Compatibility Tests

    [TestMethod]
    public void ServiceCollection_CanRegisterModelPatch_WithoutPlugins()
    {
        var services = new ServiceCollection()
                .AddLogging()
                .AddModelPatch()
                .BuildServiceProvider();

        // Just verify it registers without error
        var provider = services.GetService<PatchPluginProvider>();
        Assert.IsNotNull(provider);
    }

    [TestMethod]
    public void ServiceCollection_CanRegisterDataModelService_WithoutPlugins()
    {
        var services = new ServiceCollection()
                .AddLogging()
                .AddModelPatch()
                .AddScoped(typeof(DataModelService<>))
                .BuildServiceProvider();

        // Just verify it registers without error
        Assert.IsNotNull(services);
    }

    #endregion

    private TestDbContext CreateContext()
    {
        var contextOptions = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase($"TestDb-{Guid.NewGuid()}")
            .Options;
        var context = new TestDbContext(contextOptions);
        context.Database.EnsureCreated();
        return context;
    }

    #region Test Plugin Implementation

    /// <summary>
    /// Test plugin to verify plugin execution
    /// </summary>
    public class TestPrePatchPlugin : IPrePatchPlugin
    {
        public int Order => 50;
        public List<ModelProcessContext> ExecutedContexts { get; } = [];

        public ValueTask<IResult> Execute(ModelProcessContext context, CancellationToken cancellationToken = default)
        {
            ExecutedContexts.Add(context);
            return ValueTask.FromResult<IResult>(Result.Ok);
        }
    }

    #endregion
}
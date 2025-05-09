using CoreOne.ModelPatch;
using CoreOne.ModelPatch.Services;
using CoreOne.ModelPatch.Test.Data;
using CoreOne.ModelPatch.Test.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CoreOne.ModelPatch.Test;

[TestClass]
public class DeltaContextTest : Disposable
{
    protected TestDbContext Context { get; set; } = default!;
    protected DataModelService<TestDbContext> Service { get; set; } = default!;
    protected IServiceProvider Services { get; set; } = default!;

    public DeltaContextTest()
    {
        Context = CreateContext();
        Services = new ServiceCollection()
            .AddLogging()
            .AddScoped(typeof(DataModelService<>))
            .AddSingleton(Context)
            .BuildServiceProvider();

        Service = Services.GetRequiredService<DataModelService<TestDbContext>>();
    }

    [TestMethod]
    public async Task TestInsert()
    {
        Guid id = ID.Create();
        var delta = new Delta<Blog> {
            [nameof(Blog.BlogId).ToLower()] = id,
            [nameof(Blog.Url).ToLower()] = "myblog.com",
            [nameof(Blog.Name).ToLower()] = "MyBlog"
        };

        var result = await Service.Patch(delta);
        Assert.AreEqual(ResultType.Success, result.ResultType);
        Assert.AreEqual(id, result.Model.Model?.BlogId);

        var count = await Context.Blogs.CountAsync();
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public async Task InsertParentChild()
    {
        var post1 = new Post { PostId = ID.Create(), Content = "Content", Title = "First Post" };
        var post2 = new Post { PostId = ID.Create(), Content = "Content", Title = "Second Post" };
        var blog = new Blog {
            BlogId = ID.Create(),
            Name = "Unit1",
            Posts = [post1, post2]
        };
        var json = Utility.Serialize(blog);
        var delta = Utility.DeserializeObject<Delta<Blog>>(json);
        var result = await Service.Patch(delta!);
        Assert.AreEqual(ResultType.Success, result.ResultType);

        var data = await Context.Blogs.Include(p => p.Posts)
            .FirstOrDefaultAsync();
        Assert.AreEqual(2, data!.Posts.Count);
    }

    [TestMethod]
    public async Task UpdateModel()
    {
        var newName = "Unit2";
        var blog = new Blog {
            BlogId = ID.Create(),
            Name = "Unit1"
        };
        var delta = new Delta<Blog> {
            [nameof(Blog.BlogId).ToLower()] = blog.BlogId,
            [nameof(Blog.Name).ToLower()] = newName
        };

        Context.Blogs.Add(blog);
        await Context.SaveChangesAsync();

        var result = await Service.Patch(delta);
        Assert.AreEqual(ResultType.Success, result.ResultType);

        var source = await Context.Blogs.FirstOrDefaultAsync(p => p.BlogId == blog.BlogId);
        Assert.IsNotNull(source);
        Assert.AreEqual(newName, source.Name);
    }

    [TestMethod]
    public async Task ValidationShouldFail()
    {
        var delta = new Delta<Blog> {
            [nameof(Blog.Url).ToLower()] = "myblog.com",
            [nameof(Blog.Name).ToLower()] = "Name is too long for this blog model. Validation should fail"
        };

        var result = await Service.Patch(delta);
        Assert.AreEqual(ResultType.Fail, result.ResultType);

        var count = await Context.Blogs.CountAsync();
        Assert.AreEqual(0, count);
    }

    protected override void OnDispose()
    {
        Context.Dispose();
        base.OnDispose();
    }

    private static TestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var context = new TestDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
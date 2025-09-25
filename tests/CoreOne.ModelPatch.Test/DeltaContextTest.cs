using CoreOne.ModelPatch.Test.Data;
using CoreOne.ModelPatch.Test.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CoreOne.ModelPatch.Test;

[TestClass]
public class DeltaContextTest : Disposable
{
    protected SToken Token = SToken.Create();
    protected TestDbContext Context { get; set; } = default!;
    protected DataModelService<TestDbContext> Service { get; set; } = default!;
    protected IServiceProvider Services { get; set; } = default!;

    [TestInitialize]
    public void InitializeContext()
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
    public async Task InsertParentChild()
    {
        var post1 = new Post { PostId = ID.Create(), Content = "Content", Title = "First Post" };
        var post2 = new Post { PostId = ID.Create(), Content = "Content", Title = "Second Post" };
        var blog = new Blog {
            BlogId = ID.Create(),
            Name = "Unit1",
            Posts = [post1, post2]
        };
        var delta = ToDelta(blog);
        var result = await Service.Patch(delta!, Token);
        Assert.AreEqual(ResultType.Success, result.ResultType);

        var data = await Context.Blogs.Include(p => p.Posts)
            .FirstOrDefaultAsync(Token);
        Assert.HasCount(2, data!.Posts);
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

        var result = await Service.Patch(delta, Token);
        Assert.AreEqual(ResultType.Success, result.ResultType);
        Assert.AreEqual(id, result.Model?.Model?.BlogId);

        var count = await Context.Blogs.CountAsync(Token);
        Assert.AreEqual(1, count);

        var saved = await Context.Blogs.FirstOrDefaultAsync(p => p.BlogId == id, Token);
        Assert.AreEqual(delta[nameof(Blog.Url).ToLower()], saved!.Url);
    }

    [TestMethod]
    public async Task TestInsertIndex()
    {
        var blog = new Blog {
            BlogId = ID.Create(),
            Name = "my blog",
            Tags = [
                new Tag("tag1"),
                new Tag("tag2")
            ]
        };
        var content = Utility.Serialize(blog, true);

        var result = await Service.Patch(ToDelta(blog), Token);
        Assert.AreEqual(ResultType.Success, result.ResultType);

        var count = await Context.Tags.CountAsync(Token);
        Assert.AreEqual(2, count);

        try
        {
            blog = new Blog {
                BlogId = ID.Create(),
                Name = "another blog",
                Tags = [
                    new Tag("tag1"),
            ]
            };
            content = Utility.Serialize(blog, true);

            result = await Service.Patch(ToDelta(blog), Token);
            Assert.AreEqual(ResultType.Success, result.ResultType);

            count = await Context.Tags.CountAsync(Token);
            Assert.AreEqual(2, count);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            throw;
        }
    }

    [TestMethod]
    public async Task TestUniqueIndeces()
    {
        var tag = new Tag { Id = ID.Create(), Name = "tag" };
        var delta = ToDelta(tag);
        var result = await Service.Patch(delta, Token);
        Assert.AreEqual(ResultType.Success, result.ResultType);
    }

    [TestMethod]
    public async Task TestUniqueIndeces2()
    {
        var tag = new Tag { Id = ID.Create(), Name = "tag" };
        var tag2 = new Tag { Id = ID.Create(), Name = "tag" };

        var content = Utility.Serialize(new[] { tag, tag2 }, true);
        var result = await Service.Patch(ToDelta(tag), Token);
        Assert.AreEqual(ResultType.Success, result.ResultType);

        result = await Service.Patch(ToDelta(tag2), Token);
        Assert.AreEqual(ResultType.Success, result.ResultType);
        Assert.IsNotNull(result?.Model);
        Assert.AreEqual(result.Model?.Model?.Id, tag.Id);

        var count = await Context.Tags.CountAsync(Token);
        Assert.AreEqual(1, count);

        var tag3 = new Tag { Id = ID.Create(), Name = "tag2" };
        result = await Service.Patch(ToDelta(tag3), Token);
        Assert.AreEqual(ResultType.Success, result.ResultType);
        Assert.IsNotNull(result?.Model);
        Assert.AreEqual(result.Model?.Model?.Id, tag3.Id);

        count = await Context.Tags.CountAsync(Token);
        Assert.AreEqual(2, count);
    }

    [TestMethod]
    public async Task TestCollection()
    {
        var tag = new Tag("tag1") { Id = ID.Create() };
        var tags = new List<Tag>() {
            new Tag("tag1"){ Id = ID.Create() },
            new Tag("tag2"),
            new Tag("tag3")
        };

        var result = await Service.Patch(ToDelta(tag), Token);
        Assert.AreEqual(ResultType.Success, result.ResultType);

        var resultCollection = await Service.Patch(ToDeltaCollection(tags), Token);
        Assert.AreEqual(ResultType.Success, resultCollection.ResultType);

        var patched = resultCollection.Model;
        Assert.IsNotNull(patched);
        Assert.HasCount(3, patched);

        var count = await Context.Tags.CountAsync(Token);
        Assert.AreEqual(3, count);
    }

    [TestMethod]
    public async Task InserModelWithUniqueChild()
    {
        var blog = new Blog {
            BlogId = ID.Create(),
            Name = "Unit1",
            Tags = [
                new Tag("tag1"),
                new Tag("tag1")
            ]
        };

        var result = await Service.Patch(ToDelta(blog), Token);
        Assert.AreEqual(ResultType.Success, result.ResultType);

        var count = await Context.Tags.CountAsync(Token);
        Assert.AreEqual(1, count);
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
        await Context.SaveChangesAsync(Token);

        var result = await Service.Patch(delta, Token);
        Assert.AreEqual(ResultType.Success, result.ResultType);

        var source = await Context.Blogs.FirstOrDefaultAsync(p => p.BlogId == blog.BlogId, Token);
        Assert.IsNotNull(source);
        Assert.AreEqual(newName, source.Name);
    }

    [TestMethod]
    public async Task ValidationShouldFail()
    {
        var delta = new Delta<Blog> {
            [nameof(Blog.Url).ToLower()] = "myblog.com",
            [nameof(Blog.Name).ToLower()] = "Lorem ipsum dolor sit amet, consectetuer adipiscing."
        };

        var result = await Service.Patch(delta, Token);
        Assert.AreEqual(ResultType.Fail, result.ResultType);

        var count = await Context.Blogs.CountAsync(Token);
        Assert.AreEqual(0, count);
    }

    protected override void OnDispose()
    {
        Token.Dispose();
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

    private static DeltaCollection<T> ToDeltaCollection<T>(IEnumerable<T> model) where T : class, new() => Utility.DeserializeObject<DeltaCollection<T>>(Utility.Serialize(model))!;

    private static Delta<T> ToDelta<T>(T model) where T : class, new() => Utility.DeserializeObject<Delta<T>>(Utility.Serialize(model))!;
}
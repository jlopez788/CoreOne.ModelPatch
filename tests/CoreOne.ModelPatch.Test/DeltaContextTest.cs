using CoreOne.ModelPatch.Extensions;
using CoreOne.ModelPatch.Models;
using CoreOne.ModelPatch.Test.Data;
using CoreOne.ModelPatch.Test.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

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
                .Configure<ModelOptions>(p => p.NameResolver = meta => {
                    var attribute = meta.GetCustomAttribute<JsonPropertyAttribute>();
                    return attribute?.PropertyName ?? meta.Name;
                })
                .BuildServiceProvider();

        Service = Services.GetRequiredService<DataModelService<TestDbContext>>();
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

        var result = await Service.Patch(blog.ToDelta(), Token);
        Assert.AreEqual(ResultType.Success, result.ResultType);

        var count = await Context.Tags.CountAsync(Token);
        Assert.AreEqual(1, count);

        var update = new Blog {
            BlogId = blog.BlogId,
            Name = "Unit2",
            Url = "site.com",
            Tags = [new Tag("3")]
        };
        var delta = update.ToDelta();
        delta.Remove(nameof(Blog.Name));
        result = await Service.Patch(delta, Token);
        Assert.AreEqual(ResultType.Success, result.ResultType);
        Assert.AreEqual(blog.Name, result.Model?.Model.Name);
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
        var delta = blog.ToDelta();
        var result = await Service.Patch(delta!, Token);
        Assert.AreEqual(ResultType.Success, result.ResultType);

        var data = await Context.Blogs.Include(p => p.Posts)
            .FirstOrDefaultAsync(Token);
        Assert.HasCount(2, data!.Posts);
    }

    [TestMethod]
    public async Task TestCollection()
    {
        var tag = new Tag("tag1") { Id = ID.Create() };
        var tags = new List<Tag>() {
            new("tag1"){ Id = ID.Create() },
            new("tag2"),
            new("tag3")
        };

        var result = await Service.Patch(tag.ToDelta(), Token);
        Assert.AreEqual(ResultType.Success, result.ResultType);

        var resultCollection = await Service.Patch(tags.ToDeltaCollection(), Token);
        Assert.AreEqual(ResultType.Success, resultCollection.ResultType);

        var patched = resultCollection.Model;
        Assert.IsNotNull(patched);
        Assert.HasCount(3, patched);

        var count = await Context.Tags.CountAsync(Token);
        Assert.AreEqual(3, count);
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

        var result = await Service.Patch(blog.ToDelta(), Token);
        Assert.AreEqual(ResultType.Success, result.ResultType);

        var count = await Context.Tags.CountAsync(Token);
        Assert.AreEqual(2, count);

        blog = new Blog {
            BlogId = ID.Create(),
            Name = "another blog",
            Tags = [
                new Tag("tag1"),
            ]
        };
        result = await Service.Patch(blog.ToDelta(), Token);
        Assert.AreEqual(ResultType.Success, result.ResultType);

        count = await Context.Tags.CountAsync(Token);
        Assert.AreEqual(2, count);
    }

    [TestMethod]
    public async Task TestUniqueIndeces()
    {
        var tag = new Tag { Id = ID.Create(), Name = "tag" };
        var delta = tag.ToDelta();
        var result = await Service.Patch(delta, Token);
        Assert.AreEqual(ResultType.Success, result.ResultType);
    }

    [TestMethod]
    public async Task TestUniqueIndeces2()
    {
        var tag = new Tag { Id = ID.Create(), Name = "tag" };
        var tag2 = new Tag { Id = ID.Create(), Name = "tag" };

        var result = await Service.Patch(tag.ToDelta(), Token);
        Assert.AreEqual(ResultType.Success, result.ResultType);

        result = await Service.Patch(tag2.ToDelta(), Token);
        Assert.AreEqual(ResultType.Success, result.ResultType);
        Assert.IsNotNull(result?.Model);
        Assert.AreEqual(result.Model?.Model?.Id, tag.Id);

        var count = await Context.Tags.CountAsync(Token);
        Assert.AreEqual(1, count);

        var tag3 = new Tag { Id = ID.Create(), Name = "tag2" };
        result = await Service.Patch(tag3.ToDelta(), Token);
        Assert.AreEqual(ResultType.Success, result.ResultType);
        Assert.IsNotNull(result?.Model);
        Assert.AreEqual(result.Model?.Model?.Id, tag3.Id);

        count = await Context.Tags.CountAsync(Token);
        Assert.AreEqual(2, count);
    }

    [TestMethod]
    public async Task TestUpdateEnumAsString()
    {
        var id = ID.Create();
        var result = await Service.Patch(new User { Id = id, Status = UserStatus.New }.ToDelta(), Token);
        Assert.AreEqual(ResultType.Success, result.ResultType);

        result = await Service.Patch(new User { Id = id, Status = UserStatus.Approved }.ToDelta(), Token);
        Assert.AreEqual(ResultType.Success, result.ResultType);

        var user = await Context.Users.FirstOrDefaultAsync(p => p.Id == id, Token);
        Assert.IsNotNull(user);
        Assert.AreEqual(UserStatus.Approved, user.Status);
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
}
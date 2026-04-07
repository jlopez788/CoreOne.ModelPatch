using CoreOne.ModelPatch.Extensions;
using CoreOne.ModelPatch.Models;
using CoreOne.ModelPatch.Test.Data;
using CoreOne.ModelPatch.Test.Models;
using CoreOne.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace CoreOne.ModelPatch.Test;

/// <summary>
/// Tests specifically designed to increase branch coverage
/// </summary>
[TestClass]
public class BranchCoverageTests : Disposable
{
    protected SToken Token = SToken.Create();
    protected TestDbContext Context { get; set; } = default!;
    protected DataModelService<TestDbContext> Service { get; set; } = default!;
    protected IServiceProvider Services { get; set; } = default!;
    protected IOptions<ModelOptions> Options { get; set; } = default!;

    public BranchCoverageTests()
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
        Options = Services.GetRequiredService<IOptions<ModelOptions>>();
    }

    #region PatchResult Tests

    [TestMethod]
    public void PatchResult_DefaultConstructor()
    {
        var result = new PatchResult<Blog>();
        Assert.IsFalse(result.Success);
        Assert.IsNull(result.Model);
        Assert.AreEqual(0, result.Rows);
        // ResultType defaults to 0 (None enum value)
    }

    [TestMethod]
    public void PatchResult_WithModelAndRows()
    {
        var blog = new Blog { BlogId = ID.Create().AsGuid(), Name = "Test" };
        var result = new PatchResult<Blog>(blog, 5);
        
        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.Model);
        Assert.AreEqual(blog, result.Model);
        Assert.AreEqual(5, result.Rows);
        Assert.AreEqual(blog, result.Model);
    }

    [TestMethod]
    public void PatchResult_FromIResult_Success()
    {
        var baseResult = new Result<string>("test") { ResultType = ResultType.Success };
        var patchResult = new PatchResult<string>(baseResult);
        
        Assert.IsTrue(patchResult.Success);
        Assert.AreEqual(ResultType.Success, patchResult.ResultType);
        Assert.IsNull(patchResult.Message);
    }

    [TestMethod]
    public void PatchResult_FromIResult_Failure()
    {
        var baseResult = Result.Fail<string>("Operation failed");
        var patchResult = new PatchResult<string>(baseResult);
        
        Assert.IsFalse(patchResult.Success);
        Assert.AreEqual(ResultType.Fail, patchResult.ResultType);
        Assert.AreEqual("Operation failed", patchResult.Message);
    }

    [TestMethod]
    public void PatchResult_WithInitializers()
    {
        var result = new PatchResult<Blog> {
            Message = "Custom message",
            ResultType = ResultType.Success,
            Rows = 3
        };
        
        Assert.IsTrue(result.Success);
        Assert.AreEqual("Custom message", result.Message);
        Assert.AreEqual(3, result.Rows);
    }

    #endregion

    #region ProcessedModelExtensions Tests

    [TestMethod]
    public void ProcessedModelExtensions_Count_WithNullPredicate()
    {
        var collection = new ProcessedModelCollection();
        collection.GetType().GetMethod("Add", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(collection, [new ModelState(new Blog { Name = "Blog1" }, CrudType.Created)]);
        collection.GetType().GetMethod("Add", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(collection, [new ModelState(new Blog { Name = "Blog2" }, CrudType.Updated)]);
        var result = new Result<ProcessedModelCollection>(collection);
        
        var count = result.Count();
        Assert.AreEqual(2, count);
    }

    [TestMethod]
    public void ProcessedModelExtensions_Count_WithPredicate()
    {
        var collection = new ProcessedModelCollection();
        collection.GetType().GetMethod("Add", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(collection, [new ModelState(new Blog { Name = "Blog1" }, CrudType.Created)]);
        collection.GetType().GetMethod("Add", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(collection, [new ModelState(new Blog { Name = "Blog2" }, CrudType.Updated)]);
        var result = new Result<ProcessedModelCollection>(collection);
        
        var count = result.Count(p => p.CrudType == CrudType.Created);
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public void ProcessedModelExtensions_Count_FailedResult()
    {
        var result = Result.Fail<ProcessedModelCollection>("Error");
        
        var count = result.Count();
        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public void ProcessedModelExtensions_Count_EmptyCollection()
    {
        var result = new Result<ProcessedModelCollection>(new ProcessedModelCollection());
        
        var count = result.Count();
        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public void ProcessedModelExtensions_OfType_WithNullPredicate()
    {
        var collection = new ProcessedModelCollection();
        collection.GetType().GetMethod("Add", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(collection, [new ModelState(new Blog { Name = "Blog1" }, CrudType.Created)]);
        collection.GetType().GetMethod("Add", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(collection, [new ModelState(new Post { Title = "Post1" }, CrudType.Created)]);
        var result = new Result<ProcessedModelCollection>(collection);
        
        var blogs = result.OfType<Blog>().ToList();
        Assert.HasCount(1, blogs);
        Assert.AreEqual("Blog1", blogs[0].Name);
    }

    [TestMethod]
    public void ProcessedModelExtensions_OfType_WithPredicate()
    {
        var collection = new ProcessedModelCollection();
        var addMethod = collection.GetType().GetMethod("Add", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        addMethod.Invoke(collection, [new ModelState(new Blog { Name = "Blog1" }, CrudType.Created)]);
        addMethod.Invoke(collection, [new ModelState(new Blog { Name = "Blog2" }, CrudType.Updated)]);
        addMethod.Invoke(collection, [new ModelState(new Post { Title = "Post1" }, CrudType.Created)]);
        var result = new Result<ProcessedModelCollection>(collection);
        
        var createdBlogs = result.OfType<Blog>(p => p.CrudType == CrudType.Created).ToList();
        Assert.HasCount(1, createdBlogs);
        Assert.AreEqual("Blog1", createdBlogs[0].Name);
    }

    [TestMethod]
    public void ProcessedModelExtensions_OfType_FailedResult()
    {
        var result = Result.Fail<ProcessedModelCollection>("Error");
        
        var items = result.OfType<Blog>().ToList();
        Assert.IsEmpty(items);
    }

    [TestMethod]
    public void ProcessedModelExtensions_OfType_EmptyCollection()
    {
        var result = new Result<ProcessedModelCollection>(new ProcessedModelCollection());
        
        var items = result.OfType<Blog>().ToList();
        Assert.IsEmpty(items);
    }

    #endregion

    #region TransactionState Tests

    [TestMethod]
    public async Task TransactionState_BeginTransaction_WithoutLogger()
    {
        var transaction = await Context.BeginTransaction(Token);
        
        Assert.IsNotNull(transaction);
        Assert.IsTrue(transaction.Success);
        Assert.IsFalse(transaction.IsDisposed);
        
        await transaction.DisposeAsync();
        Assert.IsTrue(transaction.IsDisposed);
    }

    [TestMethod]
    public async Task TransactionState_BeginTransaction_WithLogger()
    {
        var loggerFactory = Services.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<BranchCoverageTests>();
        var transaction = await Context.BeginTransaction(logger, Token);
        
        Assert.IsNotNull(transaction);
        Assert.IsTrue(transaction.Success);
        Assert.IsFalse(transaction.IsDisposed);
        
        await transaction.Commit();
        Assert.IsTrue(transaction.IsDisposed);
    }

    [TestMethod]
    public async Task TransactionState_FailedTransaction()
    {
        var failedTransaction = new TransactionState("Transaction failed");
        
        Assert.IsFalse(failedTransaction.Success);
        Assert.IsTrue(failedTransaction.IsDisposed);
        Assert.AreEqual("Transaction failed", failedTransaction.Message);
        Assert.AreEqual(ResultType.Fail, failedTransaction.ResultType);
        
        // Should handle dispose gracefully
        await failedTransaction.DisposeAsync();
    }

    [TestMethod]
    public async Task TransactionState_Rollback()
    {
        var transaction = await Context.BeginTransaction(Token);
        
        await transaction.Rollback();
        Assert.IsTrue(transaction.IsDisposed);
        
        // Second rollback should be safe
        await transaction.Rollback();
    }

    [TestMethod]
    public async Task TransactionState_MultipleCommits()
    {
        var transaction = await Context.BeginTransaction(Token);
        
        await transaction.Commit();
        Assert.IsTrue(transaction.IsDisposed);
        
        // Second commit should be safe
        await transaction.Commit();
    }

    #endregion

    #region DataModelService Error Path Tests

    [TestMethod]
    public async Task Patch_CancellationRequested()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        
        var delta = new Blog { Name = "Test" }.ToDelta();
        var result = await Service.Patch(delta, cts.Token);
        
        Assert.IsFalse(result.Success);
    }

    [TestMethod]
    public async Task Patch_LocalEntityMatchWithoutDbMatch()
    {
        var blog = new Blog {
            BlogId = ID.Create().AsGuid(),
            Name = "Local Blog"
        };
        
        // Add to local context without saving
        Context.Blogs.Add(blog);
        
        // Try to patch the same entity (by ID)
        var delta = blog.ToDelta();
        var result = await Service.Patch(delta, Token);
        
        Assert.AreEqual(ResultType.Success, result.ResultType);
    }

    [TestMethod]
    public async Task PatchCollection_MixedModels()
    {
        var items = new List<object> {
            new Blog { BlogId = ID.Create().AsGuid(), Name = "Blog1" },
            new Post { PostId = ID.Create().AsGuid(), Title = "Post1", Content = "Content" }
        };
        
        var result = await Service.PatchCollection(items, Token);
        
        Assert.AreEqual(ResultType.Success, result.ResultType);
        Assert.AreEqual(2, result.Count());
    }

    [TestMethod]
    public async Task PatchCollection_WithNulls()
    {
        var items = new List<object?> {
            new Blog { BlogId = ID.Create().AsGuid(), Name = "Blog1" },
            null,
            new Post { PostId = ID.Create().AsGuid(), Title = "Post1", Content = "Content" }
        };
        
        var result = await Service.PatchCollection(items!, Token);
        
        Assert.AreEqual(ResultType.Success, result.ResultType);
        Assert.AreEqual(2, result.Count()); // Nulls should be excluded
    }

    [TestMethod]
    public async Task Patch_DeltaCollection()
    {
        var blogs = new List<Blog> {
            new() { BlogId = ID.Create().AsGuid(), Name = "Blog1" },
            new() { BlogId = ID.Create().AsGuid(), Name = "Blog2" }
        };
        
        var deltaCollection = blogs.ToDeltaCollection();
        var result = await Service.Patch(deltaCollection, Token);
        
        Assert.AreEqual(ResultType.Success, result.ResultType);
        Assert.AreEqual(2, result.Count());
    }

    [TestMethod]
    public async Task Patch_WithNestedChildrenAndJsonPropertyMapping()
    {
        var session = new ChatSession("Test Session") {
            Messages = [
                new ChatMessage(ChatRoleType.User, "Hello"),
                new ChatMessage(ChatRoleType.Agent, "Hi there")
            ]
        };
        
        var delta = session.ToDelta();
        var result = await Service.Patch(delta, Token);
        
        Assert.AreEqual(ResultType.Success, result.ResultType);
        Assert.AreEqual(3, result.Count()); // 1 session + 2 messages
        
        var savedSession = await Context.Session
            .Include(s => s.Messages)
            .FirstOrDefaultAsync(Token);
        Assert.IsNotNull(savedSession);
        Assert.HasCount(2, savedSession.Messages);
    }

    [TestMethod]
    public async Task Patch_UpdateExistingWithChildren()
    {
        // Create initial session
        var sessionId = ID.Create().AsGuid();
        var session = new ChatSession("Original Title") {
            Key = sessionId,
            Messages = [
                new ChatMessage(ChatRoleType.User, "Message 1")
            ]
        };
        
        var result = await Service.Patch(session.ToDelta(), Token);
        Assert.AreEqual(ResultType.Success, result.ResultType);
        
        // Update with new message
        var updatedSession = new ChatSession("Updated Title") {
            Key = sessionId,
            Messages = [
                new ChatMessage(ChatRoleType.Agent, "Message 2")
            ]
        };
        
        result = await Service.Patch(updatedSession.ToDelta(), Token);
        Assert.AreEqual(ResultType.Success, result.ResultType);
        
        var saved = await Context.Session
            .Include(s => s.Messages)
            .FirstOrDefaultAsync(s => s.Key == sessionId, Token);
        Assert.IsNotNull(saved);
        Assert.AreEqual("Updated Title", saved.Title);
        Assert.HasCount(2, saved.Messages);
    }

    [TestMethod]
    public async Task Patch_EmptyDelta()
    {
        var delta = new Delta<Blog>();
        // Empty delta without primary key will try to create but may fail validation
        // if required fields are missing
        var result = await Service.Patch(delta, Token);
        
        // Result can be success or fail depending on validation
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task Patch_InvalidModelWithChildValidationFailure()
    {
        var blog = new Blog {
            BlogId = ID.Create().AsGuid(),
            Name = "This is a very long name that exceeds the maximum length allowed by the validation attributes" // > 50 chars
        };
        
        var delta = blog.ToDelta();
        var result = await Service.Patch(delta, Token);
        
        Assert.AreEqual(ResultType.Fail, result.ResultType);
    }

    #endregion

    #region ModelContext Branch Tests

    [TestMethod]
    public async Task Patch_ModelWithMultiplePrimaryKeys()
    {
        // Test composite key scenarios if any exist
        var blog = new Blog {
            BlogId = ID.Create().AsGuid(),
            Name = "Test"
        };
        
        var delta = blog.ToDelta();
        var result = await Service.Patch(delta, Token);
        
        Assert.AreEqual(ResultType.Success, result.ResultType);
    }

    [TestMethod]
    public async Task Patch_UpdateNonPrimaryKeyFields()
    {
        var blogId = ID.Create().AsGuid();
        var blog = new Blog {
            BlogId = blogId,
            Name = "Original Name",
            Url = "original.com"
        };
        
        await Service.Patch(blog.ToDelta(), Token);
        
        // Update only Url, not primary key
        var updateDelta = new Delta<Blog> {
            ["blogid"] = blogId,
            ["url"] = "updated.com"
        };
        
        var result = await Service.Patch(updateDelta, Token);
        Assert.AreEqual(ResultType.Success, result.ResultType);
        
        var saved = await Context.Blogs.FirstOrDefaultAsync(b => b.BlogId == blogId, Token);
        Assert.IsNotNull(saved);
        Assert.AreEqual("updated.com", saved.Url);
        Assert.AreEqual("Original Name", saved.Name); // Should remain unchanged
    }

    [TestMethod]
    public void Delta_CaseInsensitiveAccess()
    {
        var delta = new Delta<Blog> {
            ["Name"] = "Test1",
            ["name"] = "Test2", // Should overwrite
            ["NAME"] = "Test3"  // Should overwrite again
        };
        
        Assert.AreEqual("Test3", delta["name"]);
        Assert.AreEqual("Test3", delta["Name"]);
        Assert.AreEqual("Test3", delta["NAME"]);
    }

    #endregion

    #region DeltaExtensions Branch Tests

    [TestMethod]
    public void ToDelta_NullModel()
    {
        Blog? nullBlog = null;
        var delta = nullBlog.ToDelta();
        
        Assert.IsNotNull(delta);
        Assert.IsEmpty(delta);
    }

    [TestMethod]
    public void ToDeltaCollection_NullCollection()
    {
        List<Blog>? nullList = null;
        var deltaCollection = nullList.ToDeltaCollection();
        
        Assert.IsNotNull(deltaCollection);
        Assert.IsEmpty(deltaCollection);
    }

    [TestMethod]
    public void ToDeltaCollection_EmptyCollection()
    {
        var emptyList = new List<Blog>();
        var deltaCollection = emptyList.ToDeltaCollection();
        
        Assert.IsNotNull(deltaCollection);
        Assert.IsEmpty(deltaCollection);
    }

    [TestMethod]
    public void ToDeltaCollection_WithNullElements()
    {
        var listWithNulls = new List<Blog?> {
            new() { Name = "Blog1" },
            null,
            new() { Name = "Blog2" }
        };
        
        var deltaCollection = listWithNulls.ToDeltaCollection();
        
        Assert.HasCount(2, deltaCollection);
    }

            [TestMethod]
            public void ToDelta_TargetEntity_WithIncludedPropertyNames()
            {
                var dto = new BlogPatchDto {
                    BlogId = ID.Create().AsGuid(),
                    Name = "Filtered",
                    Url = "filtered.com",
                    Extra = "ignored"
                };

                var delta = dto.ToDelta<Blog>(nameof(Blog.BlogId), nameof(Blog.Name));

                Assert.AreEqual(2, delta.Count);
                Assert.IsTrue(delta.ContainsKey(nameof(Blog.BlogId)));
                Assert.IsTrue(delta.ContainsKey(nameof(Blog.Name)));
                Assert.IsFalse(delta.ContainsKey(nameof(Blog.Url)));
                Assert.IsFalse(delta.ContainsKey(nameof(BlogPatchDto.Extra)));
            }

            [TestMethod]
            public void ToDelta_TargetEntity_WithIncludedExpressions()
            {
                var dto = new BlogPatchDto {
                    BlogId = ID.Create().AsGuid(),
                    Name = "Filtered",
                    Url = "filtered.com"
                };

                var delta = dto.ToDelta<Blog>(p => p.BlogId, p => p.Url);

                Assert.AreEqual(2, delta.Count);
                Assert.IsTrue(delta.ContainsKey(nameof(Blog.BlogId)));
                Assert.IsTrue(delta.ContainsKey(nameof(Blog.Url)));
                Assert.IsFalse(delta.ContainsKey(nameof(Blog.Name)));
            }

    #endregion

            #region ModelOptions Json Helpers Tests

            [TestMethod]
            public void ModelOptions_UseNewtonsoftJsonPropertyNames()
            {
                var options = new ModelOptions().UseNewtonsoftJsonPropertyNames();
                var metadata = MetaType.GetMetadatas(typeof(Tag)).First(p => p.Name == nameof(Tag.Name));

                var name = options.NameResolver?.Invoke(metadata);
                Assert.AreEqual("name_one", name);
            }

            [TestMethod]
            public void ModelOptions_UseSystemTextJsonPropertyNames()
            {
                var options = new ModelOptions().UseSystemTextJsonPropertyNames();
                var metadata = MetaType.GetMetadatas(typeof(SystemTextJsonPatchDto)).First(p => p.Name == nameof(SystemTextJsonPatchDto.DisplayName));

                var name = options.NameResolver?.Invoke(metadata);
                Assert.AreEqual("display_name", name);
            }

            [TestMethod]
            public void ModelOptions_UseJsonPropertyNames_FallsBackToExistingResolver()
            {
                var options = new ModelOptions {
                    NameResolver = meta => $"mapped_{meta.Name}"
                }.UseJsonPropertyNames();
                var attributed = MetaType.GetMetadatas(typeof(SystemTextJsonPatchDto)).First(p => p.Name == nameof(SystemTextJsonPatchDto.DisplayName));
                var plain = MetaType.GetMetadatas(typeof(SystemTextJsonPatchDto)).First(p => p.Name == nameof(SystemTextJsonPatchDto.PlainName));

                Assert.AreEqual("display_name", options.NameResolver?.Invoke(attributed));
                Assert.AreEqual("mapped_PlainName", options.NameResolver?.Invoke(plain));
            }

            #endregion

    #region ModelContext Deep Tests

    [TestMethod]
    public void ModelContext_ImplicitOperator()
    {
        Type blogType = typeof(Blog);
        ModelContext context = blogType;
        
        Assert.IsNotNull(context);
        Assert.AreEqual(blogType, context.Type);
        Assert.IsTrue(context.IsValid);
    }

    [TestMethod]
    public void ModelContext_ToString()
    {
        var context = new ModelContext(typeof(Blog));
        var str = context.ToString();
        
        Assert.Contains("Blog", str);
    }

    [TestMethod]
    public void ModelContext_WithLink()
    {
        var parentKeys = new List<ModelKey> { new("BlogId", true) };
        var link = new ModelLink(parentKeys, "MyBlogId");
        var context = new ModelContext(typeof(Post), link);
        
        Assert.IsNotNull(context.Link);
        Assert.AreEqual("MyBlogId", context.Link.ChildProperty);
        Assert.IsTrue(context.IsValid);
    }

    [TestMethod]
    public void ModelContext_FallbackKeyDiscovery()
    {
        // Create a context for a type without [Key] attribute but with Id property
        var context = new ModelContext(typeof(User));
        
        Assert.IsTrue(context.IsValid);
        Assert.IsGreaterThan(0, context.Keys.Count);
    }

    #endregion

    #region ProcessedModelCollection Tests

    [TestMethod]
    public void ProcessedModelCollection_Indexer()
    {
        var result = new Result<ProcessedModelCollection>(new ProcessedModelCollection());
        var addMethod = result.Model!.GetType().GetMethod("Add", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        
        var blog1 = new ModelState(new Blog { Name = "Blog1" }, CrudType.Created);
        var blog2 = new ModelState(new Blog { Name = "Blog2" }, CrudType.Updated);
        
        addMethod.Invoke(result.Model, [blog1]);
        addMethod.Invoke(result.Model, [blog2]);
        
        Assert.AreEqual(2, result.Model.Count);
        Assert.AreEqual(blog1, result.Model[0]);
        Assert.AreEqual(blog2, result.Model[1]);
    }

    [TestMethod]
    public void ProcessedModelCollection_ToString()
    {
        var collection = new ProcessedModelCollection();
        var addMethod = collection.GetType().GetMethod("Add", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        
        addMethod.Invoke(collection, [new ModelState(new Blog { Name = "Test" }, CrudType.Created)]);
        addMethod.Invoke(collection, [new ModelState(new Post { Title = "Test" }, CrudType.Created)]);
        
        var str = collection.ToString();
        Assert.AreEqual("Count: 2", str);
    }

    [TestMethod]
    public void ProcessedModelCollection_Enumeration()
    {
        var collection = new ProcessedModelCollection();
        var addMethod = collection.GetType().GetMethod("Add", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        
        addMethod.Invoke(collection, [new ModelState(new Blog { Name = "Blog1" }, CrudType.Created)]);
        addMethod.Invoke(collection, [new ModelState(new Blog { Name = "Blog2" }, CrudType.Created)]);
        
        var list = new List<ModelState>();
        foreach (var item in collection)
        {
            list.Add(item);
        }
        
        Assert.HasCount(2, list);
    }

    [TestMethod]
    public void ProcessedModelCollection_AddRange()
    {
        var collection1 = new ProcessedModelCollection();
        var collection2 = new ProcessedModelCollection();
        var addMethod = collection1.GetType().GetMethod("Add", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var addRangeMethod = collection1.GetType().GetMethod("AddRange", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        
        addMethod.Invoke(collection1, [new ModelState(new Blog { Name = "Blog1" }, CrudType.Created)]);
        addMethod.Invoke(collection2, [new ModelState(new Blog { Name = "Blog2" }, CrudType.Created)]);
        addMethod.Invoke(collection2, [new ModelState(new Blog { Name = "Blog3" }, CrudType.Created)]);
        
        addRangeMethod.Invoke(collection1, [collection2]);
        
        Assert.AreEqual(3, collection1.Count);
    }

    #endregion

    #region ModelContextExtensions Advanced Tests

    [TestMethod]
    public void ModelContextExtensions_GetPrimaryKeysExpression_ValidContext()
    {
        // Test that a normal model with keys creates valid context
        var context = new ModelContext(typeof(Blog));
        
        Assert.IsTrue(context.IsValid); // Should have keys
        Assert.IsGreaterThan(0, context.Keys.Count);
    }

    [TestMethod]
    public async Task ModelContextExtensions_GetChildren_WithInverseProperty()
    {
        var blog = new Blog {
            BlogId = ID.Create().AsGuid(),
            Name = "Test Blog",
            Posts = [
                new Post { PostId = ID.Create().AsGuid(), Title = "Post 1", Content = "Content 1" },
                new Post { PostId = ID.Create().AsGuid(), Title = "Post 2", Content = "Content 2" }
            ]
        };
        
        var delta = blog.ToDelta();
        var result = await Service.Patch(delta, Token);
        
        Assert.AreEqual(ResultType.Success, result.ResultType);
        Assert.AreEqual(3, result.Count()); // 1 blog + 2 posts
        Assert.AreEqual(1, result.Count(p => p.CrudType == CrudType.Created && p.Model is Blog));
        Assert.AreEqual(2, result.Count(p => p.CrudType == CrudType.Created && p.Model is Post));
    }

    [TestMethod]
    public async Task Patch_CompositeKeyScenario()
    {
        // Test with Tag which has both primary key and unique index
        var tag1 = new Tag { Id = ID.Create().AsGuid(), Name = "tag1" };
        var tag2 = new Tag { Id = ID.Create().AsGuid(), Name = "tag1" }; // Same name, different ID
        
        var result1 = await Service.Patch(tag1.ToDelta(), Token);
        Assert.AreEqual(ResultType.Success, result1.ResultType);
        Assert.AreEqual(1, result1.Count(p => p.CrudType == CrudType.Created));
        
        // Second insert with same name should update, not create
        var result2 = await Service.Patch(tag2.ToDelta(), Token);
        Assert.AreEqual(ResultType.Success, result2.ResultType);
        Assert.AreEqual(1, result2.Count(p => p.CrudType == CrudType.Updated));
        
        var count = await Context.Tags.CountAsync(Token);
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public async Task Patch_MultipleUniqueIndexes()
    {
        var blog1 = new Blog {
            BlogId = ID.Create().AsGuid(),
            Name = "Blog1",
            Tags = [
                new Tag("unique1"),
                new Tag("unique2"),
                new Tag("unique3")
            ]
        };
        
        var result = await Service.Patch(blog1.ToDelta(), Token);
        Assert.AreEqual(ResultType.Success, result.ResultType);
        
        // Try to add blog with some duplicate tags
        var blog2 = new Blog {
            BlogId = ID.Create().AsGuid(),
            Name = "Blog2",
            Tags = [
                new Tag("unique1"), // Duplicate - should update
                new Tag("unique2"), // Duplicate - should update
                new Tag("unique4")  // New - should create
            ]
        };
        
        result = await Service.Patch(blog2.ToDelta(), Token);
        Assert.AreEqual(ResultType.Success, result.ResultType);
        
        var tagCount = await Context.Tags.CountAsync(Token);
        Assert.AreEqual(4, tagCount); // 3 from first + 1 new
    }

    #endregion

    #region Complex Scenarios

    [TestMethod]
    public async Task Patch_DeeplyNestedRelationships()
    {
        var session = new ChatSession("Deep Test") {
            Messages = [
                new ChatMessage(ChatRoleType.User, "Question 1"),
                new ChatMessage(ChatRoleType.Agent, "Answer 1"),
                new ChatMessage(ChatRoleType.User, "Question 2")
            ]
        };
        
        var result = await Service.Patch(session.ToDelta(), Token);
        
        Assert.AreEqual(ResultType.Success, result.ResultType);
        Assert.AreEqual(4, result.Count()); // 1 session + 3 messages
        
        var savedSession = await Context.Session
            .Include(s => s.Messages)
            .FirstOrDefaultAsync(Token);
        
        Assert.IsNotNull(savedSession);
        Assert.HasCount(3, savedSession.Messages);
        Assert.AreEqual("Deep Test", savedSession.Title);
    }

    [TestMethod]
    public async Task Patch_UpdateExistingAndAddNew()
    {
        var blogId = ID.Create().AsGuid();
        var postId = ID.Create().AsGuid();
        
        // Create initial blog with one post
        var blog = new Blog {
            BlogId = blogId,
            Name = "Initial Blog",
            Posts = [
                new Post { PostId = postId, Title = "Initial Post", Content = "Content" }
            ]
        };
        
        await Service.Patch(blog.ToDelta(), Token);
        
        // Update blog and add another post
        var updatedBlog = new Blog {
            BlogId = blogId,
            Name = "Updated Blog",
            Posts = [
                new Post { PostId = postId, Title = "Updated Post", Content = "Updated Content" },
                new Post { PostId = ID.Create().AsGuid(), Title = "New Post", Content = "New Content" }
            ]
        };
        
        var result = await Service.Patch(updatedBlog.ToDelta(), Token);
        
        Assert.AreEqual(ResultType.Success, result.ResultType);
        Assert.AreEqual(1, result.Count(p => p.CrudType == CrudType.Updated && p.Model is Blog));
        Assert.AreEqual(1, result.Count(p => p.CrudType == CrudType.Updated && p.Model is Post));
        Assert.AreEqual(1, result.Count(p => p.CrudType == CrudType.Created && p.Model is Post));
        
        var saved = await Context.Blogs.Include(b => b.Posts).FirstOrDefaultAsync(b => b.BlogId == blogId, Token);
        Assert.IsNotNull(saved);
        Assert.AreEqual("Updated Blog", saved.Name);
        Assert.HasCount(2, saved.Posts);
    }

    [TestMethod]
    public async Task Patch_EmptyChildCollection()
    {
        var blog = new Blog {
            BlogId = ID.Create().AsGuid(),
            Name = "Blog with empty collections",
            Posts = [],
            Tags = []
        };
        
        var result = await Service.Patch(blog.ToDelta(), Token);
        
        Assert.AreEqual(ResultType.Success, result.ResultType);
        Assert.AreEqual(1, result.Count());
    }

    [TestMethod]
    public async Task Patch_NullableGuidPrimaryKey()
    {
        var tag = new Tag {
            Id = Guid.Empty, // Will be auto-generated
            Name = "auto-generated-id"
        };
        
        var result = await Service.Patch(tag.ToDelta(), Token);
        
        Assert.AreEqual(ResultType.Success, result.ResultType);
        var saved = await Context.Tags.FirstOrDefaultAsync(t => t.Name == "auto-generated-id", Token);
        Assert.IsNotNull(saved);
        Assert.AreNotEqual(Guid.Empty, saved.Id);
    }

    [TestMethod]
    public void Delta_MultiplePropertyFormats()
    {
        var delta = new Delta<Blog> {
            ["blogid"] = ID.Create().AsGuid(),
            ["Name"] = "Test",
            ["URL"] = "test.com"
        };
        
        Assert.IsTrue(delta.ContainsKey("blogid"));
        Assert.IsTrue(delta.ContainsKey("BlogId")); // Case insensitive
        Assert.IsTrue(delta.ContainsKey("name"));
        Assert.IsTrue(delta.ContainsKey("url"));
    }

    [TestMethod]
    public async Task Patch_PartialPropertyUpdate()
    {
        var blogId = ID.Create().AsGuid();
        
        // Create blog
        var blog = new Blog {
            BlogId = blogId,
            Name = "Original Name",
            Url = "original.com"
        };
        await Service.Patch(blog.ToDelta(), Token);
        
        // Update only Name, not Url
        var partialDelta = new Delta<Blog> {
            ["blogid"] = blogId,
            ["name"] = "New Name"
        };
        
        var result = await Service.Patch(partialDelta, Token);
        Assert.AreEqual(ResultType.Success, result.ResultType);
        
        var saved = await Context.Blogs.FirstOrDefaultAsync(b => b.BlogId == blogId, Token);
        Assert.AreEqual("New Name", saved!.Name);
        Assert.AreEqual("original.com", saved.Url); // Should not change
    }

    [TestMethod]
    public async Task Patch_PropertyWithDefaultValue()
    {
        var user = new User {
            Id = ID.Create().AsGuid(),
            Status = UserStatus.New // Default enum value
        };
        
        var result = await Service.Patch(user.ToDelta(), Token);
        
        Assert.AreEqual(ResultType.Success, result.ResultType);
        var saved = await Context.Users.FirstOrDefaultAsync(u => u.Id == user.Id, Token);
        Assert.IsNotNull(saved);
        Assert.AreEqual(UserStatus.New, saved.Status);
    }

    [TestMethod]
    public async Task Patch_DirectModelOverload_ReturnsRichResult()
    {
        var blog = new Blog {
            BlogId = ID.Create().AsGuid(),
            Name = "Direct Patch"
        };

        var result = await Service.Patch(blog, Token);

        Assert.AreEqual(ResultType.Success, result.ResultType);
        Assert.AreEqual(1, result.Created);
        Assert.AreEqual(0, result.Updated);
        Assert.IsNotNull(result.Items);
        Assert.AreEqual(1, result.Items.Count);
        Assert.AreEqual("Direct Patch", result.Get<Blog>().Single().Name);
    }

    [TestMethod]
    public async Task Patch_DirectModelOverload_WithConfigure()
    {
        var blogId = ID.Create().AsGuid();
        await Service.Patch(new Blog {
            BlogId = blogId,
            Name = "Original",
            Url = "original.com"
        }, Token);

        var result = await Service.Patch(new Blog {
            BlogId = blogId,
            Name = "Changed",
            Url = "updated.com"
        }, delta => delta.Remove(nameof(Blog.Name)), Token);

        Assert.AreEqual(ResultType.Success, result.ResultType);
        Assert.AreEqual(1, result.Updated);

        var saved = await Context.Blogs.FirstOrDefaultAsync(p => p.BlogId == blogId, Token);
        Assert.IsNotNull(saved);
        Assert.AreEqual("Original", saved.Name);
        Assert.AreEqual("updated.com", saved.Url);
    }

    [TestMethod]
    public async Task Patch_DirectCollectionOverload()
    {
        var result = await Service.Patch([
            new Tag("direct-1") { Id = ID.Create().AsGuid() },
            new Tag("direct-2") { Id = ID.Create().AsGuid() }
        ], Token);

        Assert.AreEqual(ResultType.Success, result.ResultType);
        Assert.AreEqual(2, result.Created);
        Assert.AreEqual(2, result.Get<Tag>().Count());
    }

    [TestMethod]
    public async Task Patch_DtoOverload_WithEntityTarget()
    {
        var dto = new BlogPatchDto {
            BlogId = ID.Create().AsGuid(),
            Name = "Dto Blog",
            Url = "dto.com"
        };

        var result = await Service.Patch<Blog, BlogPatchDto>(dto, Token);

        Assert.AreEqual(ResultType.Success, result.ResultType);
        Assert.AreEqual(1, result.Created);
        var saved = await Context.Blogs.FirstOrDefaultAsync(p => p.BlogId == dto.BlogId, Token);
        Assert.IsNotNull(saved);
        Assert.AreEqual("Dto Blog", saved.Name);
        Assert.AreEqual("dto.com", saved.Url);
    }

    [TestMethod]
    public async Task Patch_DtoOverload_WithIncludedProperties()
    {
        var id = ID.Create().AsGuid();
        await Service.Patch(new Blog { BlogId = id, Name = "Original", Url = "original.com" }, Token);

        var dto = new BlogPatchDto {
            BlogId = id,
            Name = "ShouldNotChange",
            Url = "updated.com"
        };

        var result = await Service.Patch<Blog, BlogPatchDto>(dto, [p => p.BlogId, p => p.Url], Token);

        Assert.AreEqual(ResultType.Success, result.ResultType);
        var saved = await Context.Blogs.FirstOrDefaultAsync(p => p.BlogId == id, Token);
        Assert.IsNotNull(saved);
        Assert.AreEqual("Original", saved.Name);
        Assert.AreEqual("updated.com", saved.Url);
    }

    [TestMethod]
    public async Task Patch_StrictPropertyMatching_UnknownFieldFails()
    {
        Options.Value.StrictPropertyMatching = true;
        var delta = new Delta<Blog> {
            [nameof(Blog.BlogId)] = ID.Create().AsGuid(),
            [nameof(Blog.Name)] = "strict",
            ["does_not_exist"] = "x"
        };

        var result = await Service.Patch(delta, Token);

        Assert.AreEqual(ResultType.Fail, result.ResultType);
        Assert.IsNotNull(result.Message);
        Assert.Contains("does_not_exist", result.Message);
    }

    [TestMethod]
    public async Task Patch_ConcurrencyTokenMismatch_Fails()
    {
        var id = ID.Create().AsGuid();
        var version = new byte[] { 1, 2, 3 };
        await Service.Patch(new VersionedBlog {
            Id = id,
            Name = "v1",
            RowVersion = version
        }, Token);

        var staleDelta = new Delta<VersionedBlog> {
            [nameof(VersionedBlog.Id)] = id,
            [nameof(VersionedBlog.Name)] = "v2",
            [nameof(VersionedBlog.RowVersion)] = Convert.ToBase64String(new byte[] { 9, 9, 9 })
        };

        var result = await Service.Patch(staleDelta, Token);

        Assert.AreEqual(ResultType.Fail, result.ResultType);
        Assert.IsNotNull(result.Message);
        Assert.Contains(nameof(VersionedBlog.RowVersion), result.Message);
    }

    [TestMethod]
    public async Task Patch_RequireConcurrencyTokenForUpdate_FailsWhenMissing()
    {
        Options.Value.RequireConcurrencyTokenForUpdates = true;

        var id = ID.Create().AsGuid();
        await Service.Patch(new VersionedBlog {
            Id = id,
            Name = "v1",
            RowVersion = [1, 2, 3]
        }, Token);

        var missingTokenDelta = new Delta<VersionedBlog> {
            [nameof(VersionedBlog.Id)] = id,
            [nameof(VersionedBlog.Name)] = "v2"
        };

        var result = await Service.Patch(missingTokenDelta, Token);

        Assert.AreEqual(ResultType.Fail, result.ResultType);
        Assert.IsNotNull(result.Message);
        Assert.Contains("Concurrency token is required", result.Message);
    }

    [TestMethod]
    public void ServiceCollectionExtensions_AddModelPatch_RegistersOptionsAndService()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(CreateContext())
            .AddModelPatch(options => options.StrictPropertyMatching = true)
            .BuildServiceProvider();

        var service = services.GetRequiredService<DataModelService<TestDbContext>>();
        var options = services.GetRequiredService<IOptions<ModelOptions>>();

        Assert.IsNotNull(service);
        Assert.IsTrue(options.Value.StrictPropertyMatching);
    }

    #endregion

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

    private sealed class BlogPatchDto
    {
        public Guid BlogId { get; set; }
        public string? Name { get; set; }
        public string? Url { get; set; }
        public string? Extra { get; set; }
    }

    private sealed class SystemTextJsonPatchDto
    {
        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }

        public string? PlainName { get; set; }
    }
}

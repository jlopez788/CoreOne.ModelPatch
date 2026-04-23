# CoreOne.ModelPatch

A powerful .NET 9 / .NET 10 library for applying partial updates (PATCH operations) to EF Core entities. It converts partial model data into `Delta<T>` objects and intelligently patches entities while handling nested relationships, unique constraints, and parent-child foreign key relationships.

Perfect for RESTful APIs, microservices, and any scenario requiring partial entity updates without manual property-by-property mapping.

## 🚀 Key Features

- **Selective Property Updates** - Only update properties present in the delta, ignoring missing fields
- **Nested Relationship Handling** - Automatically processes parent-child relationships with foreign key injection
- **Unique Constraint Support** - Respects `[Index(IsUnique = true)]` attributes (updates existing records instead of duplicating)
- **Transaction Management** - Wraps all operations in EF Core transactions with automatic rollback on errors
- **Composite Key Support** - Handles entities with single or composite primary keys
- **Case-Insensitive Delta Properties** - Property name matching is case-insensitive for flexibility
- **Built-In JSON Naming Helpers** - Configure Newtonsoft.Json or System.Text.Json property names without hand-written reflection lambdas
- **Custom Property Name Mapping** - Support custom property name resolution through `ModelOptions.NameResolver`
- **Strict Field Validation (Optional)** - Fail fast on unknown delta fields to catch typos early
- **Optimistic Concurrency (Optional)** - Validate `[Timestamp]` and `[ConcurrencyCheck]` tokens during updates
- **Extensible Plugin Pipeline** - Ordered pre/post patch plugins for validation and custom behavior
- **Optional Multi-Tenancy Package** - Tenant-aware pre-patch validation and tenant key injection
- **Automatic Key Generation** - Generates primary keys when missing (GUID by default, extensible via `IKeyGenerator`)
- **Type-Safe Delta Operations** - Strongly-typed `Delta<T>` with compile-time safety
- **DTO-Friendly Delta Mapping** - Map request DTOs into `Delta<TEntity>` with explicit field selection
- **Rich Patch Results** - `PatchResult` exposes `Created`, `Updated`, `Unchanged`, `Items`, and `Get<T>()`

## 📦 Installation

Install via NuGet:

```bash
dotnet add package CoreOne.ModelPatch
```

Or via the NuGet Package Manager:

```powershell
Install-Package CoreOne.ModelPatch
```

Optional tenant support package:

```bash
dotnet add package CoreOne.ModelPatch.Tenants
```

**Requirements:**
- net9.0 or net10.0
- EF Core 9.0.9+ on net9.0, EF Core 10.0.5+ on net10.0
- CoreOne 1.4.0.6+ (automatically installed as dependency)

## 🏗️ Setup

### 1. Register Services in DI Container

```csharp
// In Program.cs or Startup.cs
services.AddDbContext<YourDbContext>(options => 
    options.UseSqlServer(connectionString));

services.AddModelPatch(options => {
    options.UseJsonPropertyNames();
    options.StrictPropertyMatching = true; // optional fail-fast mode
});
```

### 2. Inject into Your Services/Controllers

```csharp
public class YourController : ControllerBase
{
    private readonly IDataModelService<YourDbContext> _dataService;

    public YourController(IDataModelService<YourDbContext> dataService)
    {
        _dataService = dataService;
    }
    
    // ... use _dataService in your endpoints
}
```

## 🛠️ Usage Examples

### Basic PATCH Operation

```csharp
// Your entity model
public class User
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string? PhoneNumber { get; set; }
}

// Create a partial update model
var userUpdate = new User {
    Id = existingUserId,
    Name = "John Doe",
    Email = "john@example.com"
};

// Apply the patch directly and optionally exclude fields from the generated delta
var result = await _dataService.Patch(userUpdate, delta => {
    delta.Remove(nameof(User.PhoneNumber));
}, cancellationToken);

if (result.ResultType == ResultType.Success)
{
    var updated = result.Updated;
    Console.WriteLine($"Updated {updated} records");
}
```

### Parent-Child Relationships

The library automatically handles nested relationships:

```csharp
public class Blog
{
    [Key] public Guid BlogId { get; set; }
    [Required] public string Name { get; set; }
    
    [InverseProperty(nameof(Tag.Blog))]
    public List<Tag> Tags { get; init; } = [];
}

[Index(nameof(Name), IsUnique = true)]
public class Tag
{
    [Key] public Guid Id { get; set; }
    public string Name { get; set; }
    
    public Guid? BlogId { get; set; }
    [ForeignKey(nameof(BlogId))]
    public Blog? Blog { get; set; }
}

// Create blog with nested tags
var blog = new Blog {
    BlogId = Guid.NewGuid(),
    Name = "Tech Blog",
    Tags = [
        new Tag { Name = "CSharp" },
        new Tag { Name = "DotNet" },
        new Tag { Name = "CSharp" } // Duplicate - will update existing due to unique index
    ]
};

var result = await _dataService.Patch(blog, cancellationToken);

// The library will:
// 1. Insert/update the Blog
// 2. Process each Tag
// 3. Automatically set BlogId foreign key in each Tag
// 4. Respect unique index (only 2 tags created: CSharp and DotNet)
```

### Unique Index Constraint Handling

When entities have unique indexes, the library updates existing records instead of creating duplicates:

```csharp
[Index(nameof(Email), IsUnique = true)]
public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; }
    public string Name { get; set; }
}

// If a user with email "john@example.com" already exists:
var newUser = new User {
    Email = "john@example.com",
    Name = "John Updated"
};

var result = await _dataService.Patch(newUser, cancellationToken);

// Instead of throwing a duplicate key error, the existing user is updated
var updated = result.Get<User>().FirstOrDefault();
```

### Multiple Models (Collection Patching)

```csharp
// Patch multiple models of the same type directly
var users = new List<User> {
    new User { Id = id1, Name = "Alice" },
    new User { Id = id2, Name = "Bob" }
};

var result = await _dataService.Patch(users, cancellationToken);

// Or patch mixed model types
var mixedModels = new List<object> { user1, blog1, tag1 };
var result = await _dataService.PatchCollection(mixedModels, cancellationToken);
```

### DTO-First PATCH Mapping

```csharp
public class UpdateBlogRequest
{
    public Guid BlogId { get; set; }
    public string? Name { get; set; }
    public string? Url { get; set; }
    public string? IgnoredByPatch { get; set; }
}

var request = new UpdateBlogRequest {
    BlogId = blogId,
    Name = "Updated title",
    Url = "https://example.com"
};

// Keep only the target entity fields you want to patch
var delta = request.ToDelta<Blog>(p => p.BlogId, p => p.Name, p => p.Url);
var result = await _dataService.Patch(delta, cancellationToken);

// Or patch DTO directly with target entity type
var directResult = await _dataService.Patch<Blog, UpdateBlogRequest>(request, cancellationToken);

// Note: direct DTO patch maps DTO fields into a delta by matching names.
// Use the explicit include-list overload when you want strict field allow-listing.
var safeResult = await _dataService.Patch<Blog, UpdateBlogRequest>(
    request,
    [p => p.BlogId, p => p.Name, p => p.Url],
    cancellationToken);
```

### Optimistic Concurrency Example

```csharp
public class Blog
{
    [Key] public Guid BlogId { get; set; }
    public string Name { get; set; } = string.Empty;

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];
}

services.AddModelPatch(options => {
    options.ValidateConcurrencyTokens = true;
    options.RequireConcurrencyTokenForUpdates = true; // optional stricter mode
});

var delta = new Delta<Blog> {
    [nameof(Blog.BlogId)] = blogId,
    [nameof(Blog.Name)] = "Updated",
    [nameof(Blog.RowVersion)] = Convert.ToBase64String(rowVersionFromClient)
};

var result = await _dataService.Patch(delta, cancellationToken);
// Fails with ResultType.Fail when token is stale or missing (if required)

// Supported incoming token formats include:
// - byte[]
// - base64 string (for byte[] tokens)
// - IEnumerable<byte>
```

## 🌐 ASP.NET Core Web API Integration

### PATCH Endpoint Example

```csharp
[ApiController]
[Route("api/[controller]")]
public class BlogsController : ControllerBase
{
    private readonly IDataModelService<AppDbContext> _dataService;

    public BlogsController(IDataModelService<AppDbContext> dataService)
    {
        _dataService = dataService;
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> PatchBlog(
        Guid id, 
        [FromBody] Blog patchData, 
        CancellationToken cancellationToken)
    {
        // Ensure the ID matches
        patchData.BlogId = id;
        
        var result = await _dataService.Patch(patchData, cancellationToken);

        if (result.ResultType == ResultType.Success)
        {
            return Ok(new {
                message = "Blog updated successfully",
                created = result.Created,
                updated = result.Updated,
                items = result.Items
            });
        }

        return BadRequest(new { 
            error = result.Message 
        });
    }

    [HttpPatch("bulk")]
    public async Task<IActionResult> PatchMultipleBlogs(
        [FromBody] List<Blog> blogs, 
        CancellationToken cancellationToken)
    {
        var result = await _dataService.Patch(blogs, cancellationToken);

        if (result.ResultType == ResultType.Success)
        {
            var stats = new {
                created = result.Created,
                updated = result.Updated,
                unchanged = result.Unchanged
            };
            return Ok(new { message = "Bulk update completed", stats });
        }

        return BadRequest(new { error = result.Message });
    }
}
```

### Handling Validation Errors

The library automatically rolls back transactions when validation fails:

```csharp
public class Blog
{
    [Key] public Guid BlogId { get; set; }
    
    [Required, StringLength(50)] 
    public string Name { get; set; }
    
    [Url, StringLength(200)] 
    public string? Url { get; set; }
}

// If validation fails (e.g., Name exceeds 50 chars), 
// the entire transaction is rolled back automatically
var result = await _dataService.Patch(delta, cancellationToken);

if (result.ResultType == ResultType.Fail)
{
    // Handle validation error
    return BadRequest(result.Message);
}
```

## 🔧 Advanced Configuration

### Built-In JSON Property Name Helpers

Use the built-in helpers when your models already declare JSON names:

```csharp
services.Configure<ModelOptions>(options => {
    options.UseNewtonsoftJsonPropertyNames();
    // or options.UseSystemTextJsonPropertyNames();
    // or options.UseJsonPropertyNames(); // supports both attribute types
});

// Example entity with JSON property mapping
public class Tag
{
    [Key] public Guid Id { get; set; }
    
    [JsonProperty("tag_name")]  // Maps "tag_name" in JSON to Name property
    public string Name { get; set; }
}

// Now you can use either property name in your delta
var delta = new Delta<Tag> {
    ["tag_name"] = "CSharp",  // Works!
    ["Name"] = "CSharp"        // Also works! (case-insensitive)
};
```

You can still provide a custom `NameResolver` when you need non-attribute-based naming.

### Default Options

`ModelOptions` defaults are:

- `StrictPropertyMatching = false`
- `ValidateConcurrencyTokens = true`
- `RequireConcurrencyTokenForUpdates = false`
- `KeyGenerator = GuidGenerator` (creates version 7 GUID keys wrapped in `KeyModel`)

### Strict Field Validation

```csharp
services.AddModelPatch(options => {
    options.StrictPropertyMatching = true;
});

// Unknown fields will fail with a clear message instead of being ignored.
```

### Working with PatchResult

The `Patch` methods return `PatchResult`, which provides summary counts and typed access to processed entities:

```csharp
var result = await _dataService.Patch(delta, cancellationToken);

if (result.ResultType == ResultType.Success)
{
    var created = result.Created;
    var updated = result.Updated;
    var unchanged = result.Unchanged;
    var blogs = result.Get<Blog>();
    var newTags = result.Get<Tag>(p => p.CrudType == CrudType.Created);
}
```

### Custom Key Generation

Implement `IKeyGenerator` for custom primary key generation:

```csharp
public class CustomKeyGenerator : IKeyGenerator
{
    public KeyModel Create() => KeyModel.Create(Guid.CreateVersion7());
}

services.Configure<ModelOptions>(options => {
    options.KeyGenerator = new CustomKeyGenerator();
});
```

### Strongly Typed IDs (`ICoreId<T>`)

`AddModelPatch(...)` also registers an open generic `IKeyGenerator<TKey>` implementation (`StronglyTypedIdGenerator<TKey>`), which can be used for strongly typed IDs backed by GUID values.

```csharp
[StronglyTypedId<Guid>]
public readonly partial struct NoteId : ICoreId<NoteId>
{
}

// Example custom generator for NoteId
public sealed class NoteIdKeyGenerator : IKeyGenerator<NoteId>
{
    public KeyModel Create() => KeyModel.Create(NoteId.Create());
}
```

For runtime patching, the built-in primary-key auto-generation path currently targets `Guid`/`Guid?` key properties.
For other key property types, include the key value in the incoming delta/model payload or provide a custom patch pipeline.

### Plugin Pipeline

Plugins execute in descending order by `Order` and can fail the patch early.

Built-in plugins registered by `AddModelPatch(...)`:

- `StrictPropertyValidationPlugin` (`IPrePatchPlugin`, `Order = 1001`)
- `ConcurrencyTokenValidationPlugin` (`IPrePatchPlugin`, `Order = 800`)
- `ModelStateValidationPlugin` (`IPostPatchPlugin`, `Order = 1000`)

Register custom plugins using DI enumerable registration:

```csharp
services.AddModelPatch();
services.TryAddEnumerable(ServiceDescriptor.Scoped<IPrePatchPlugin, MyCustomPrePatchPlugin>());
services.TryAddEnumerable(ServiceDescriptor.Scoped<IPostPatchPlugin, MyCustomPostPatchPlugin>());
```

### Multi-Tenancy (Optional Package)

`CoreOne.ModelPatch.Tenants` adds a high-priority pre-patch tenant plugin (`Order = 9999`) to enforce tenant ownership and inject tenant keys.

```csharp
services.AddModelPatch();

// Default HttpContext tenant provider
services.AddTenantSupport(options => {
    options.AutoInjectTenantId = true;
    options.ThrowOnTenantMismatch = true;
});
```

Or register a custom tenant provider:

```csharp
services.AddTenantSupport<MyTenantProvider>(options => {
    options.AutoInjectTenantId = true;
    options.ThrowOnTenantMismatch = true;
});
```

Mark tenant-bound entities with CoreOne.Identity attributes:

```csharp
[TenantOwned]
public class Blog
{
    [Key] public Guid Id { get; set; }

    [TenantKey]
    public string TenantId { get; set; } = string.Empty;
}
```

## 🎯 How It Works

### The Delta Pattern

`Delta` is a case-insensitive dictionary that holds partial model data:

```csharp
// Delta inherits from Data<string, object> (case-insensitive dictionary)
var delta = new Delta<User> {
    ["name"] = "John",      // Case-insensitive
    ["Name"] = "John",      // Same as above
    ["EMAIL"] = "john@example.com"
};

// Or convert from model
var user = new User { Name = "John", Email = "john@example.com" };
var delta = user.ToDelta();

// Or map from a DTO into an entity delta
var request = new { blogId = blogId, name = "Updated title", url = "https://example.com" };
var blogDelta = request.ToDelta<Blog>(nameof(Blog.BlogId), nameof(Blog.Name), nameof(Blog.Url));

// Remove unwanted properties
delta.Remove("CreatedAt");
delta.Remove("UpdatedAt");
```

### Processing Workflow

1. **Delta Conversion** - Convert your model to `Delta<T>` using `.ToDelta()`
2. **Transaction Begin** - Library wraps operation in EF Core transaction
3. **Entity Resolution** - Finds existing entities by primary/unique keys
4. **Pre-Patch Plugins** - Runs ordered pre-patch plugins (strict fields, concurrency, custom plugins)
5. **Property Patching** - Updates only properties present in delta
6. **Relationship Processing** - Recursively processes child collections
7. **Foreign Key Injection** - Automatically sets parent foreign keys in children
8. **Unique Constraint Handling** - Updates existing records with unique values
9. **Post-Patch Plugins** - Runs ordered post-patch plugins (model validation, custom plugins)
10. **Transaction Commit** - Saves all changes atomically (or rolls back on error)

### Key Discovery Rules

Entity identity is discovered in this order:

1. Properties marked with `[Key]`
2. Unique index definitions from `[Index(..., IsUnique = true)]`
3. Convention fallback names: `Id`, `Key`, `{EntityName}Id`, `{EntityName}Key`

### Relationship Discovery

The library uses reflection and EF Core attributes to discover relationships:

- **`[InverseProperty]`** - Links parent to child collections
- **`[ForeignKey]`** - Identifies foreign key properties
- **Convention-based** - Falls back to `{ParentName}Id` pattern

```csharp
public class Blog
{
    [Key] public Guid BlogId { get; set; }
    
    // InverseProperty tells the library where this collection is referenced
    [InverseProperty(nameof(Post.Blog))]
    public List<Post> Posts { get; init; } = [];
}

public class Post
{
    [Key] public Guid Id { get; set; }
    
    // ForeignKey identifies the foreign key property
    public Guid? BlogId { get; set; }
    [ForeignKey(nameof(BlogId))]
    public Blog? Blog { get; set; }
}
```

## 📋 API Reference

### IDataModelService<TContext>

Main service contract for processing patches.

#### Interface Methods

```csharp
// Patch a single typed delta
Task<PatchResult> Patch<T>(
    Delta<T> delta, 
    CancellationToken cancellationToken = default) where T : class, new()

// Patch multiple deltas of the same type
Task<PatchResult> Patch<T>(
    DeltaCollection<T> items, 
    CancellationToken cancellationToken = default) where T : class, new()

// Patch mixed model types
Task<PatchResult> PatchCollection(
    IEnumerable<object?> items, 
    CancellationToken cancellationToken = default)
```

#### Extension Patch Methods

The following overloads are extension methods on `IDataModelService<TContext>` (from `DataModelServiceExtensions`):

```csharp
// Patch a single model directly
Task<PatchResult> Patch<TContext, TModel>(
    this IDataModelService<TContext> service,
    TModel model,
    CancellationToken cancellationToken = default)
    where TContext : DbContext where TModel : class, new()

// Patch a single model directly and customize generated delta
Task<PatchResult> Patch<TContext, TModel>(
    this IDataModelService<TContext> service,
    TModel model,
    Action<Delta<TModel>> configure,
    CancellationToken cancellationToken = default)
    where TContext : DbContext where TModel : class, new()

// Patch a DTO into a target entity
Task<PatchResult> Patch<TContext, TEntity, TDto>(
    this IDataModelService<TContext> service,
    TDto? dto,
    CancellationToken cancellationToken = default)
    where TContext : DbContext where TEntity : class, new()

// Patch DTO with delta customization callback
Task<PatchResult> Patch<TContext, TEntity, TDto>(
    this IDataModelService<TContext> service,
    TDto? dto,
    Action<Delta<TEntity>> configure,
    CancellationToken cancellationToken = default)
    where TContext : DbContext where TEntity : class, new()

// Patch DTO with explicit included entity properties
Task<PatchResult> Patch<TContext, TEntity, TDto>(
    this IDataModelService<TContext> service,
    TDto? dto,
    IEnumerable<Expression<Func<TEntity, object?>>> includedProperties,
    CancellationToken cancellationToken = default)
    where TContext : DbContext where TEntity : class, new()

// Patch a collection of model instances directly
Task<PatchResult> Patch<TContext, TModel>(
    this IDataModelService<TContext> service,
    IEnumerable<TModel?> items,
    CancellationToken cancellationToken = default)
    where TContext : DbContext where TModel : class, new()

// Patch a collection of model instances directly with delta customization
Task<PatchResult> Patch<TContext, TModel>(
    this IDataModelService<TContext> service,
    IEnumerable<TModel?> items,
    Action<Delta<TModel>> configure,
    CancellationToken cancellationToken = default)
    where TContext : DbContext where TModel : class, new()

// Patch a collection of DTO payloads
Task<PatchResult> Patch<TContext, TEntity, TDto>(
    this IDataModelService<TContext> service,
    IEnumerable<TDto?> items,
    CancellationToken cancellationToken = default)
    where TContext : DbContext where TEntity : class, new()

// Patch a collection of DTO payloads with explicit included entity properties
Task<PatchResult> Patch<TContext, TEntity, TDto>(
    this IDataModelService<TContext> service,
    IEnumerable<TDto?> items,
    IEnumerable<Expression<Func<TEntity, object?>>> includedProperties,
    CancellationToken cancellationToken = default)
    where TContext : DbContext where TEntity : class, new()
```

### Extension Methods

```csharp
// Convert model to Delta<T>
Delta<T> ToDelta<T>(this T model) where T : class, new()

// Convert collection to DeltaCollection<T>
DeltaCollection<T> ToDeltaCollection<T>(this IEnumerable<T?> items) where T : class, new()

// Map a DTO into a target entity delta
Delta<TEntity> ToDelta<TEntity>(this object model) where TEntity : class, new()

// Map a DTO into a target entity delta using an explicit field list
Delta<TEntity> ToDelta<TEntity>(this object model, params string[] includedFields) where TEntity : class, new()

// Map a DTO into a target entity delta using entity property expressions
Delta<TEntity> ToDelta<TEntity>(this object model, params Expression<Func<TEntity, object?>>[] includedProperties) where TEntity : class, new()

// Filter a patch result
IEnumerable<T> Get<T>(Predicate<ModelState>? predicate = null)
int Count(Predicate<ModelState>? predicate = null)
```

### CrudType Enum

Indicates what operation was performed:

```csharp
public enum CrudType
{
    Created = 1,
    Read = 2,
    Updated = 4,
    Deleted = 8
}
```

## ⚠️ Important Notes

### Transaction Behavior

- All operations are wrapped in transactions
- Validation failures automatically rollback the entire transaction
- Nested operations are part of the same transaction
- Use cancellation tokens to cancel long-running operations

### Performance Considerations

- The library uses reflection and caches metadata for performance
- Composite keys and unique indexes are discovered once per type
- For bulk operations, use `DeltaCollection<T>` or `PatchCollection` instead of individual calls

### Limitations

- Entities must have parameterless constructors
- Navigation properties must be settable (at least `init` accessors)
- Circular references in object graphs may cause issues (design your models carefully)
- Currently supports INSERT and UPDATE operations (DELETE planned for future)

## 🧰 Troubleshooting Cookbook

### 1) Strict mode fails with unknown field errors

**Symptom**

- `ResultType.Fail` with a message similar to: `Unknown fields for Blog: does_not_exist`

**Likely cause**

- `StrictPropertyMatching` is enabled and the incoming payload contains fields that do not map to the target entity.
- The payload uses DTO names that differ from entity names without JSON name mapping.

**Fix**

```csharp
services.AddModelPatch(options => {
    options.StrictPropertyMatching = true;
    options.UseJsonPropertyNames(); // if your model uses JsonProperty/JsonPropertyName
});

// Option A: send only entity field names
var delta = new Delta<Blog> {
    [nameof(Blog.BlogId)] = id,
    [nameof(Blog.Name)] = "Updated"
};

// Option B: map DTO to entity delta with explicit include list
var deltaFromDto = request.ToDelta<Blog>(p => p.BlogId, p => p.Name, p => p.Url);
```

### 2) Concurrency mismatch on update

**Symptom**

- `ResultType.Fail` with a message similar to: `Concurrency token mismatch for Blog.RowVersion`

**Likely cause**

- The client is sending an outdated token value for a model using `[Timestamp]` or `[ConcurrencyCheck]`.

**Fix**

```csharp
services.AddModelPatch(options => {
    options.ValidateConcurrencyTokens = true;
    options.RequireConcurrencyTokenForUpdates = true;
});

var delta = new Delta<Blog> {
    [nameof(Blog.BlogId)] = blogId,
    [nameof(Blog.Name)] = "Updated",
    [nameof(Blog.RowVersion)] = Convert.ToBase64String(currentRowVersionFromClient)
};

var result = await dataService.Patch(delta, token);
```

### 3) Update fails because concurrency token is required

**Symptom**

- `ResultType.Fail` with a message similar to: `Concurrency token is required for updates to Blog`

**Likely cause**

- `RequireConcurrencyTokenForUpdates` is enabled for a model that has concurrency tokens, but the payload omitted the token.

**Fix**

- Include token values on update requests for concurrency-enabled models.
- If your API does not require this strict behavior, set `RequireConcurrencyTokenForUpdates = false`.

### 4) DTO patch updates missing or wrong fields

**Symptom**

- Patch succeeds but expected fields are unchanged, or wrong fields are updated.

**Likely cause**

- DTO property names don’t match entity names.
- DTO includes fields that should not be patched.

**Fix**

```csharp
// Recommended: target entity + explicit allowed fields
var result = await dataService.Patch<Blog, UpdateBlogRequest>(
    request,
    [p => p.BlogId, p => p.Name, p => p.Url],
    token);

// Alternative: generate delta then trim
var delta = request.ToDelta<Blog>(p => p.BlogId, p => p.Name, p => p.Url);
var patchResult = await dataService.Patch(delta, token);
```

### 5) Unique index behavior seems unexpected (update vs insert)

**Symptom**

- Sending a new object appears to update an existing row instead of inserting a new one.

**Likely cause**

- Target entity has a unique index (`[Index(..., IsUnique = true)]`), and an existing row already matches the unique key.

**Fix**

- This is expected behavior for duplicate unique-key payloads.
- If insert-only semantics are required for a flow, validate uniqueness before calling `Patch` or use a dedicated insert endpoint.

### Quick triage checklist

- Confirm target entity key and unique index values in the payload.
- Confirm DTO-to-entity field mapping (`UseJsonPropertyNames` or explicit include list).
- Confirm strict mode and concurrency options in `AddModelPatch(...)`.
- Inspect `result.Message`, `result.Created`, `result.Updated`, `result.Unchanged`, and `result.Get<T>()`.

## ⬆️ Upgrade Notes

### Current Notes

- `IKeyGenerator.Create()` now returns `KeyModel`.
- `IKeyGenerator<TKey>` support is available for strongly typed key scenarios.
- `AddModelPatch(...)` registers `IDataModelService<TContext>`, `IKeyGenerator`, and open-generic `IKeyGenerator<TKey>`.
- Concurrency token validation options (`ValidateConcurrencyTokens`, `RequireConcurrencyTokenForUpdates`) are part of `ModelOptions`.

### Current 1.4.0.2 Notes

- Core package version is `1.4.0.2`.
- CoreOne dependency version is `1.4.0.6`.
- Optional tenant package is `CoreOne.ModelPatch.Tenants` (`0.9.0`).

## 🧪 Testing

The library includes comprehensive unit and integration tests:

```bash
# Run tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

See [COVERAGE_REPORT.md](COVERAGE_REPORT.md) for detailed coverage metrics.

## 📊 Test Coverage

- Unit and integration tests run against both `net9.0` and `net10.0`
- Current suite covers direct patching, DTO mapping, JSON naming helpers, unique indexes, and parent-child processing

## 🔗 Dependencies

- **[CoreOne](https://www.nuget.org/packages/CoreOne/)** (1.4.0.6+) - Provides `Data<TKey, TValue>`, `IResult<T>`, reflection utilities
- **Microsoft.EntityFrameworkCore** 9.0.9+ / 10.0.5+ - EF Core framework matched to the target framework
- **[CoreOne.ModelPatch.Tenants](https://www.nuget.org/packages/CoreOne.ModelPatch.Tenants/)** (optional) - Tenant-aware pre-patch validation and tenant injection

## 🤝 Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Write tests for your changes
4. Ensure all tests pass (`dotnet test`)
5. Commit your changes (`git commit -m 'Add amazing feature'`)
6. Push to the branch (`git push origin feature/amazing-feature`)
7. Open a Pull Request

## 🐛 Issues & Support

- **GitHub Issues:** [https://github.com/jlopez788/CoreOne.ModelPatch/issues](https://github.com/jlopez788/CoreOne.ModelPatch/issues)
- **Documentation:** See `.github/copilot-instructions.md` for AI-friendly development guide

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Built on top of [CoreOne](https://github.com/jlopez788/CoreOne) utility library
- Uses Entity Framework Core for data access
- Inspired by JSON Patch (RFC 6902) but adapted for EF Core entities

---

**Author:** Juan Lopez  
**Version:** 1.4.0.2
**Target Frameworks:** net9.0, net10.0

# CoreOne.ModelPatch

A powerful .NET 9 library for applying partial updates (PATCH operations) to EF Core entities. Converts partial model data into `Delta<T>` objects and intelligently patches entities while handling nested relationships, unique constraints, and parent-child foreign key relationships.

Perfect for RESTful APIs, microservices, and any scenario requiring partial entity updates without manual property-by-property mapping.

## 🚀 Key Features

- **Selective Property Updates** - Only update properties present in the delta, ignoring missing fields
- **Nested Relationship Handling** - Automatically processes parent-child relationships with foreign key injection
- **Unique Constraint Support** - Respects `[Index(IsUnique = true)]` attributes (updates existing records instead of duplicating)
- **Transaction Management** - Wraps all operations in EF Core transactions with automatic rollback on errors
- **Composite Key Support** - Handles entities with single or composite primary keys
- **Case-Insensitive Delta Properties** - Property name matching is case-insensitive for flexibility
- **Custom Property Name Mapping** - Support for JSON property names via `ModelOptions.NameResolver`
- **Automatic GUID Generation** - Generates primary keys when missing
- **Type-Safe Delta Operations** - Strongly-typed `Delta<T>` with compile-time safety

## 📦 Installation

Install via NuGet:

```bash
dotnet add package CoreOne.ModelPatch
```

Or via the NuGet Package Manager:

```powershell
Install-Package CoreOne.ModelPatch
```

**Requirements:**
- .NET 9.0 or higher
- Microsoft.EntityFrameworkCore 9.0.9 or compatible
- CoreOne 1.3.0 (automatically installed as dependency)

## 🏗️ Setup

### 1. Register Services in DI Container

```csharp
// In Program.cs or Startup.cs
services.AddDbContext<YourDbContext>(options => 
    options.UseSqlServer(connectionString));

services.AddScoped(typeof(DataModelService<>));

// Optional: Configure custom property name resolution
services.Configure<ModelOptions>(options => {
    options.NameResolver = metadata => {
        // Example: Support JSON property name attributes
        var jsonAttr = metadata.GetCustomAttribute<JsonPropertyAttribute>();
        return jsonAttr?.PropertyName ?? metadata.Name;
    };
});
```

### 2. Inject into Your Services/Controllers

```csharp
public class YourController : ControllerBase
{
    private readonly DataModelService<YourDbContext> _dataService;

    public YourController(DataModelService<YourDbContext> dataService)
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
    // PhoneNumber is not set, so it won't be updated
};

// Convert to Delta and optionally exclude properties
var delta = userUpdate.ToDelta();
delta.Remove("PhoneNumber"); // Explicitly exclude from update

// Apply the patch
var result = await _dataService.Patch(delta, cancellationToken);

if (result.ResultType == ResultType.Success)
{
    var processedModels = result.Data;
    // Check what was updated
    var updated = processedModels.Count(p => p.CrudType == CrudType.Updated);
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

var delta = blog.ToDelta();
var result = await _dataService.Patch(delta, cancellationToken);

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

var result = await _dataService.Patch(newUser.ToDelta(), cancellationToken);

// Instead of throwing a duplicate key error, the existing user is updated
var updated = result.Data.FirstOrDefault(p => p.CrudType == CrudType.Updated);
```

### Multiple Models (Collection Patching)

```csharp
// Patch multiple models of the same type
var users = new List<User> {
    new User { Id = id1, Name = "Alice" },
    new User { Id = id2, Name = "Bob" }
};

var deltaCollection = new DeltaCollection<User>();
users.ForEach(u => deltaCollection.Add(u.ToDelta()));

var result = await _dataService.Patch(deltaCollection, cancellationToken);

// Or patch mixed model types
var mixedModels = new List<object> { user1, blog1, tag1 };
var result = await _dataService.PatchCollection(mixedModels, cancellationToken);
```

## 🌐 ASP.NET Core Web API Integration

### PATCH Endpoint Example

```csharp
[ApiController]
[Route("api/[controller]")]
public class BlogsController : ControllerBase
{
    private readonly DataModelService<AppDbContext> _dataService;

    public BlogsController(DataModelService<AppDbContext> dataService)
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
        
        var delta = patchData.ToDelta();
        var result = await _dataService.Patch(delta, cancellationToken);

        if (result.ResultType == ResultType.Success)
        {
            return Ok(new {
                message = "Blog updated successfully",
                data = result.Data
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
        var deltaCollection = new DeltaCollection<Blog>();
        blogs.ForEach(b => deltaCollection.Add(b.ToDelta()));

        var result = await _dataService.Patch(deltaCollection, cancellationToken);

        if (result.ResultType == ResultType.Success)
        {
            var stats = new {
                created = result.Data.Count(p => p.CrudType == CrudType.Created),
                updated = result.Data.Count(p => p.CrudType == CrudType.Updated),
                unchanged = result.Data.Count(p => p.CrudType == CrudType.Unchanged)
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

if (result.ResultType == ResultType.Failure)
{
    // Handle validation error
    return BadRequest(result.Message);
}
```

## 🔧 Advanced Configuration

### Custom Property Name Resolution

Support for JSON property attributes or custom naming strategies:

```csharp
services.Configure<ModelOptions>(options => {
    options.NameResolver = metadata => {
        // Support Newtonsoft.Json [JsonProperty] attributes
        var jsonAttr = metadata.GetCustomAttribute<JsonPropertyAttribute>();
        if (jsonAttr?.PropertyName != null)
            return jsonAttr.PropertyName;
            
        // Support System.Text.Json [JsonPropertyName] attributes
        var jsonPropertyName = metadata.GetCustomAttribute<JsonPropertyNameAttribute>();
        if (jsonPropertyName?.Name != null)
            return jsonPropertyName.Name;
            
        // Default to property name
        return metadata.Name;
    };
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

### Working with ProcessedModelCollection

The `Patch` methods return `IResult<ProcessedModelCollection>` which provides insights into what was processed:

```csharp
var result = await _dataService.Patch(delta, cancellationToken);

if (result.ResultType == ResultType.Success)
{
    var processed = result.Data;
    
    // Filter by CRUD type
    var created = processed.Count(p => p.CrudType == CrudType.Created);
    var updated = processed.Count(p => p.CrudType == CrudType.Updated);
    var unchanged = processed.Count(p => p.CrudType == CrudType.Unchanged);
    
    // Filter by model type
    var blogs = processed.OfType<Blog>();
    var tags = processed.OfType<Tag>();
    
    // Custom filtering
    var recentlyCreated = processed
        .Where(p => p.CrudType == CrudType.Created)
        .OfType<Blog>();
}
```

### Custom Key Generation

Implement `IKeyGenerator` for custom primary key generation:

```csharp
public class CustomKeyGenerator : IKeyGenerator
{
    public object? Generate(Type type)
    {
        if (type == typeof(Guid))
            return Guid.NewGuid();
            
        if (type == typeof(int))
            return 0; // Let DB generate
            
        // Add custom logic for other types
        return null;
    }
}

// Register in DI
services.AddSingleton<IKeyGenerator, CustomKeyGenerator>();
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

// Remove unwanted properties
delta.Remove("CreatedAt");
delta.Remove("UpdatedAt");
```

### Processing Workflow

1. **Delta Conversion** - Convert your model to `Delta<T>` using `.ToDelta()`
2. **Transaction Begin** - Library wraps operation in EF Core transaction
3. **Entity Resolution** - Finds existing entities by primary/unique keys
4. **Property Patching** - Updates only properties present in delta
5. **Relationship Processing** - Recursively processes child collections
6. **Foreign Key Injection** - Automatically sets parent foreign keys in children
7. **Unique Constraint Handling** - Updates existing records with unique values
8. **Validation** - EF Core validates changes
9. **Transaction Commit** - Saves all changes atomically (or rolls back on error)

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

### DataModelService<TContext>

Main service for processing patches.

#### Methods

```csharp
// Patch a single model
Task<IResult<ProcessedModelCollection>> Patch<T>(
    Delta<T> delta, 
    CancellationToken cancellationToken = default) where T : class, new()

// Patch multiple models of the same type
Task<IResult<ProcessedModelCollection>> Patch<T>(
    DeltaCollection<T> items, 
    CancellationToken cancellationToken = default) where T : class, new()

// Patch mixed model types
Task<IResult<ProcessedModelCollection>> PatchCollection(
    IEnumerable<object> items, 
    CancellationToken cancellationToken = default)
```

### Extension Methods

```csharp
// Convert model to Delta<T>
Delta<T> ToDelta<T>(this T model) where T : class, new()

// Convert collection to DeltaCollection<T>
DeltaCollection<T> ToDelta<T>(this IEnumerable<T> items) where T : class, new()

// Filter ProcessedModelCollection
IEnumerable<T> OfType<T>(this ProcessedModelCollection collection)
int Count(this ProcessedModelCollection collection, Func<ModelState, bool> predicate)
```

### CrudType Enum

Indicates what operation was performed:

```csharp
public enum CrudType
{
    Unchanged = 0,  // No changes made
    Created = 1,    // New entity created
    Updated = 2,    // Existing entity updated
    Deleted = 3     // Entity deleted (future)
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

## 🧪 Testing

The library includes comprehensive unit tests with >93% code coverage:

```bash
# Run tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

See [COVERAGE_REPORT.md](COVERAGE_REPORT.md) for detailed coverage metrics.

## 📊 Test Coverage

- **Line Coverage:** 93.1%
- **Branch Coverage:** 80.8%
- **Total Tests:** 64
- All critical paths thoroughly tested

## 🔗 Dependencies

- **[CoreOne](https://www.nuget.org/packages/CoreOne/)** (1.3.0+) - Provides `Data<TKey, TValue>`, `IResult<T>`, reflection utilities
- **Microsoft.EntityFrameworkCore** (9.0.9+) - EF Core framework

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
**Version:** 1.2.4  
**Target Framework:** .NET 9.0
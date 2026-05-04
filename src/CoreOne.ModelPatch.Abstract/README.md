# CoreOne.ModelPatch.Abstract

The abstractions package for [CoreOne.ModelPatch](https://www.nuget.org/packages/CoreOne.ModelPatch) — providing the interfaces, models, and core types needed to build and extend partial update (PATCH) pipelines for EF Core entities.

> Install this package when you want to reference the CoreOne.ModelPatch contracts without taking a dependency on the full implementation — for example, in a shared domain or plugin library.

## 📦 Installation

```bash
dotnet add package CoreOne.ModelPatch.Abstract
```

**Requirements:** net9.0 or net10.0

---

## What's Included

### Delta Types

`Delta` and `Delta<T>` are case-insensitive dictionaries that hold only the properties you explicitly include in a partial update. The generic version gives you compile-time type safety.

```csharp
// Untyped — useful for dynamic scenarios
var delta = new Delta();
delta["Name"] = "Alice";
delta["Email"] = "alice@example.com";

// Typed — tied to a specific entity
var typed = new Delta<User>();
typed["Name"] = "Alice";
```

`DeltaCollection<T>` is a list of `Delta<T>` for batch operations.

---

### IDataModelService\<TContext\>

The primary service interface your application uses to apply patches. Inject this to avoid a hard dependency on the implementation assembly.

```csharp
public class UserService(IDataModelService<AppDbContext> patchService)
{
    public Task<PatchResult> PatchUser(Delta<User> delta, CancellationToken ct)
        => patchService.Patch(delta, ct);
}
```

Methods:

| Method | Description |
|---|---|
| `Patch<T>(Delta<T>, ct)` | Patch a single entity |
| `Patch<T>(DeltaCollection<T>, ct)` | Patch a batch of entities |
| `PatchCollection(IEnumerable<object?>, ct)` | Patch a mixed collection of untyped objects |

---

### Plugin Interfaces

Implement these to inject custom logic into the patch pipeline.

| Interface | When it runs | Typical uses |
|---|---|---|
| `IPrePatchPlugin` | Before the delta is applied | Validation, enrichment, tenant injection |
| `IPostPatchPlugin` | After the delta is applied | Model-state validation, auditing |

Both interfaces expose an `Order` property (higher = runs first). Implement them and register with DI:

```csharp
// Custom pre-patch plugin
public class AuditPlugin : IPrePatchPlugin
{
    public int Order => 500;

    public ValueTask<IResult> Execute(ModelProcessContext context, CancellationToken ct)
    {
        context.Delta["UpdatedAt"] = DateTime.UtcNow;
        return ValueTask.FromResult(Result.Ok);
    }
}

// Register
services.TryAddEnumerable(ServiceDescriptor.Scoped<IPrePatchPlugin, AuditPlugin>());
```

---

### IKeyGenerator

Implement this to provide custom primary key generation for your entities.

```csharp
// Use the strongly-typed generic form
public class SequentialIdGenerator : IKeyGenerator<int>
{
    private static int _counter = 0;

    public int Create() => Interlocked.Increment(ref _counter);

    public KeyModel CreateKey() => new KeyModel(Create());
}
```

Register per entity type in `ModelOptions`:

```csharp
services.Configure<ModelOptions>(o =>
    o.KeyGenerators[typeof(MyEntity)] = new SequentialIdGenerator());
```

---

### ModelOptions

Controls patching behavior. Configure via `services.Configure<ModelOptions>(...)` or pass directly.

| Property | Type | Default | Description |
|---|---|---|---|
| `StrictPropertyMatching` | `bool` | `false` | Fail when delta contains a field not on the entity |
| `ValidateConcurrencyTokens` | `bool` | `true` | Check `[Timestamp]`/`[ConcurrencyCheck]` tokens on update |
| `RequireConcurrencyTokenForUpdates` | `bool` | `false` | Require a token to be present in update deltas |
| `NameResolver` | `Func<Metadata, string>?` | `null` | Map entity properties to delta key names (e.g., JSON attribute names) |
| `IgnoreFields` | `DataHashSet<Type, string>` | empty | Properties to skip during patching, per entity type |
| `KeyGenerators` | `Data<Type, IKeyGenerator>` | empty | Per-type primary key generators |
| `ExcludePlugins` | `HashSet<Type>` | empty | Plugin types to skip for this context |

---

### PatchResult

Returned by every patch call. Inspect it to determine what was created, updated, or left unchanged.

```csharp
var result = await patchService.Patch(delta, ct);

if (result.ResultType == ResultType.Success)
{
    Console.WriteLine($"Created: {result.Created}");
    Console.WriteLine($"Updated: {result.Updated}");

    // Retrieve a specific patched entity
    var user = result.Get<User>();
}
```

---

### PatchRestrictAttribute

Apply to entity properties to control whether they may be included in a patch delta.

```csharp
public class User
{
    public Guid Id { get; set; }
    public string Name { get; set; }

    // Cannot be changed via patch — silently dropped from delta
    [PatchRestrict(PatchRestrictionType.DenyUpdateSilently)]
    public DateTime CreatedAt { get; set; }

    // If present in delta, the entire patch fails with a 400-like error
    [PatchRestrict(PatchRestrictionType.DenyUpdateBadRequest)]
    public string Role { get; set; }
}
```

| Value | Behavior |
|---|---|
| `DenyUpdateSilently` | Property is removed from the delta; patch continues normally |
| `DenyUpdateBadRequest` | Patch fails if this property appears in the delta |

---

### ModelProcessContext

Passed to every plugin. Gives access to the current delta, entity type, entity instance, and CRUD state.

```csharp
public ValueTask<IResult> Execute(ModelProcessContext context, CancellationToken ct)
{
    // The partial update data
    var delta = context.Delta;

    // The CLR type being patched
    var entityType = context.Type;

    // The tracked EF Core entity (may be null before it's resolved)
    var entity = context.Model;

    // Whether this is a Create or Update
    var state = context.State; // CrudType.Created | CrudType.Updated
    return ValueTask.FromResult(Result.Ok);
}
```

---

## Related Packages

| Package | Description |
|---|---|
| [CoreOne.ModelPatch](https://www.nuget.org/packages/CoreOne.ModelPatch) | Full implementation — register with `services.AddModelPatch()` |
| [CoreOne.ModelPatch.Tenants](https://www.nuget.org/packages/CoreOne.ModelPatch.Tenants) | Optional multi-tenancy plugin for tenant key injection and validation |

---

## License

MIT — see [LICENSE](https://github.com/jlopez788/CoreOne.ModelPatch/blob/main/LICENSE) on GitHub.

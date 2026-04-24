# CoreOne.ModelPatch AI Coding Instructions

## Project Overview
CoreOne.ModelPatch is a .NET 9/10 library (v1.5.0) that enables partial entity updates (PATCH operations) in EF Core applications. It converts partial model data into `Delta<T>` objects and intelligently patches EF Core entities while handling nested relationships, unique constraints, and parent-child foreign key relationships.

Three NuGet packages:
- **CoreOne.ModelPatch** (v1.5.0) — core implementation
- **CoreOne.ModelPatch.Abstract** (v1.0.0) — interfaces and models
- **CoreOne.ModelPatch.Tenants** (v1.0.0) — optional multi-tenancy support

## Architecture & Core Components

### Delta Pattern
- `Delta`: Case-insensitive dictionary (`Data<string, object>`) that holds partial model data
- `Delta<TModel>`: Typed wrapper for model-specific deltas
- Conversion: Use `.ToDelta()` extension method to convert models to deltas
- Property names are case-insensitive throughout the system

### DataModelService<TContext>
Primary service for processing patches. Key workflow:
1. Accepts `Delta<T>` or `DeltaCollection<T>`
2. Wraps operations in EF Core transactions via `CreateExecutionStrategy()` for retry support
3. Runs pre-patch plugins (strict validation, concurrency, attribute restrictions, custom)
4. Recursively processes parent-child relationships
5. Respects unique index constraints (existing records found by unique index are updated, not duplicated)
6. Runs post-patch plugins (model state validation, custom)
7. Auto-generates GUIDs for primary keys when missing
8. Returns `PatchResult` containing `ModelState` items with CRUD operation types

### ModelContext System
Internal reflection-based model analysis:
- **ModelContext**: Caches metadata for entity types (properties, keys, relationships)
- **ModelKey**: Represents primary/unique keys with `IsPrimaryKey` flag
- **ModelLink**: Connects parent-child relationships via foreign key properties
- Uses `InversePropertyAttribute` and `ForeignKeyAttribute` to discover relationships

### ModelOptions Key Properties
- `KeyGenerator: IKeyGenerator` — default primary key generator (GuidGenerator by default)
- `KeyGenerators: Data<Type, IKeyGenerator>` — per-type generators, checked before `KeyGenerator`
- `NameResolver: Func<Metadata, string>?` — custom property name mapping (JSON attributes)
- `IgnoreFields: DataHashSet<Type, string>` — properties to exclude from patches per entity type
- `Comparer: Data<Type, IEqualityComparer>` — type-specific equality comparers
- `ExcludePlugins: HashSet<Type>` — plugin types to skip for this context
- `StrictPropertyMatching: bool` — fail on unknown delta fields (default: false)
- `ValidateConcurrencyTokens: bool` — validate `[Timestamp]`/`[ConcurrencyCheck]` tokens (default: true)
- `RequireConcurrencyTokenForUpdates: bool` — require token when tokens configured (default: false)

## Critical Patterns

### Unique Index Handling
**The library automatically detects and respects `[Index(IsUnique = true)]` attributes:**
```csharp
[Index(nameof(Name), IsUnique = true)]
public class Tag { 
    public Guid Id { get; set; }
    public string Name { get; set; }
}
```
When patching, if a record with the unique value exists, it's **updated** rather than inserting a duplicate. See [tests/CoreOne.ModelPatch.Test/DeltaContextTest.cs](tests/CoreOne.ModelPatch.Test/DeltaContextTest.cs#L157-L233) for test examples.

### Parent-Child Relationship Processing
Child collections are processed automatically when present in delta:
1. Parent model is patched first, primary keys extracted to `NamedKey`
2. Child deltas are extracted from JSON arrays
3. `ModelLink` discovers foreign key property using `[ForeignKey]` or convention (`{ParentName}Id`)
4. Foreign key values are injected into children from parent keys
5. Each child is recursively processed via `ProcessUnknownModel`

See [src/CoreOne.ModelPatch/Services/DataModelService.cs](src/CoreOne.ModelPatch/Services/DataModelService.cs#L186-L198).

### Property Name Resolution
**ModelOptions.NameResolver** allows custom property name mappings:
```csharp
services.Configure<ModelOptions>(p => p.NameResolver = meta => {
    var attr = meta.GetCustomAttribute<JsonPropertyAttribute>();
    return attr?.PropertyName ?? meta.Name;
});
```
Used in tests to map `[JsonProperty("name_one")]` to "name_one" in deltas. See [tests/CoreOne.ModelPatch.Test/DeltaContextTest.cs](tests/CoreOne.ModelPatch.Test/DeltaContextTest.cs#L27-L30).

### Primary Key Expression Building
The library builds LINQ expressions dynamically to query entities:
- Extracts all key properties from `ModelContext.Keys`
- Supports composite keys (multiple keys joined with OR)
- Uses delta values to build equality comparisons
- See [src/CoreOne.ModelPatch/Extensions/ModelContextExtensions.cs](src/CoreOne.ModelPatch/Extensions/ModelContextExtensions.cs#L61-L95)

### Plugin Pipeline
Plugins execute in **descending** order by `Order`. Built-in plugins registered by `AddModelPatch(...)`:
- `StrictPropertyValidationPlugin` (`IPrePatchPlugin`, Order: 1001) — fails on unknown delta fields when `StrictPropertyMatching = true`
- `ConcurrencyTokenValidationPlugin` (`IPrePatchPlugin`, Order: 800) — validates concurrency tokens
- `ModelAttributeValidationPlugin` (`IPrePatchPlugin`, Order: 100) — handles `[PatchRestrict]` attributes
- `ModelStateValidationPlugin` (`IPostPatchPlugin`, Order: 1000) — runs model state validation after patch

Register custom plugins with `TryAddEnumerable(ServiceDescriptor.Scoped<IPrePatchPlugin, MyPlugin>())`.

Use `ModelOptions.ExcludePlugins` to disable specific plugin types per context.

### PatchRestrict Attribute
`[PatchRestrict(PatchRestrictionType)]` on entity properties controls patch behavior:
- `PatchRestrictionType.DenyUpdateSilently` — field is silently removed from delta before processing
- `PatchRestrictionType.DenyUpdateBadRequest` — presence of field in delta causes patch failure

Processed by `ModelAttributeValidationPlugin` (Order: 100).

## CoreOne Dependency
This library depends on `CoreOne` (NuGet package), which provides:
- **Collections**: `Data<TKey, TValue>` (case-insensitive dictionary), `DataList`, `DataHashSet`
- **Extensions**: `.Each()`, `.AggregateResultAsync()`, `.SelectResultAsync()`, `.ExcludeNulls()`
- **Reflection**: `MetaType`, `Metadata` for property introspection
- **Results**: `IResult<T>`, `Result<T>` for functional error handling
- **Services**: `BaseService` for DI integration

Global usings are in [src/CoreOne.ModelPatch/GUsings.cs](src/CoreOne.ModelPatch/GUsings.cs).

## Testing Conventions

### Test Structure
- Tests use **MSTest** framework
- In-memory EF Core database with `DbContextOptions<TestDbContext>`
- Each test creates isolated context via `CreateContext()` helper
- Service setup includes ModelOptions configuration for JSON property mapping

### Common Test Patterns
```csharp
var delta = model.ToDelta();
delta.Remove("UnwantedField"); // Exclude properties from patch
var result = await Service.Patch(delta, Token);
Assert.AreEqual(ResultType.Success, result.ResultType);
Assert.AreEqual(1, result.Count(p => p.CrudType == CrudType.Created));
```

### Running Tests
```powershell
dotnet test
```
Or use VS Code Test Explorer (MSTest adapter automatically discovered).

## Build & Release

### Build Commands
```powershell
dotnet build                          # Debug build
dotnet build -c Release              # Release build with NuGet package generation
```

### NuGet Package
- **Release builds automatically generate packages** (see [src/CoreOne.ModelPatch/CoreOne.ModelPatch.csproj](src/CoreOne.ModelPatch/CoreOne.ModelPatch.csproj#L22-L23))
- Package metadata: Version, Authors, License (MIT) in `.csproj`
- README.md is included in package via `<PackageReadmeFile>`

## Code Style Notes
- **Nullable reference types enabled** (`<Nullable>enable</Nullable>`)
- Uses latest C# language version
- XML `<summary>` documentation on public API members
- Heavy use of expression-bodied members and pattern matching
- Local functions preferred for scoped helper logic (see `PatchModel` method)

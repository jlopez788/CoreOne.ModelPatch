# CoreOne.ModelPatch AI Coding Instructions

## Project Overview
CoreOne.ModelPatch is a .NET 9 library that enables partial entity updates (PATCH operations) in EF Core applications. It converts partial model data into `Delta<T>` objects and intelligently patches EF Core entities while handling nested relationships, unique constraints, and parent-child foreign key relationships.

## Architecture & Core Components

### Delta Pattern
- `Delta`: Case-insensitive dictionary (`Data<string, object>`) that holds partial model data
- `Delta<TModel>`: Typed wrapper for model-specific deltas
- Conversion: Use `.ToDelta()` extension method to convert models to deltas
- Property names are case-insensitive throughout the system

### DataModelService<TContext>
Primary service for processing patches. Key workflow:
1. Accepts `Delta<T>` or `DeltaCollection<T>`
2. Wraps operations in EF Core transactions
3. Recursively processes parent-child relationships
4. Respects unique index constraints (existing records found by unique index are updated, not duplicated)
5. Auto-generates GUIDs for primary keys when missing
6. Returns `ProcessedModelCollection` with CRUD operation types

### ModelContext System
Internal reflection-based model analysis:
- **ModelContext**: Caches metadata for entity types (properties, keys, relationships)
- **ModelKey**: Represents primary/unique keys with `IsPrimaryKey` flag
- **ModelLink**: Connects parent-child relationships via foreign key properties
- Uses `InversePropertyAttribute` and `ForeignKeyAttribute` to discover relationships

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
- Minimal XML documentation (mostly empty `<summary>` tags)
- Heavy use of expression-bodied members and pattern matching
- Local functions preferred for scoped helper logic (see `PatchModel` method)

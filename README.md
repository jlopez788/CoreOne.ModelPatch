# CoreOne.ModelPatch

A lightweight and flexible library for applying partial updates to entities in .NET Core and EF Core. 
Ideal for scenarios where PATCH operations are needed, such as RESTful APIs.

## 🚀 Features

- Apply partial updates to EF Core entities using JSON Patch-like syntax
- Supports nested properties and complex object graphs
- Easy integration with ASP.NET Core Web APIs
- Reduces boilerplate code for update operations
- Respect unique index contrainsts while inserting objects

## 📦 Installation

Install via NuGet:

```bash
dotnet add package CoreOne.ModelPatch
```

Or via the NuGet Package Manager:

```powershell
Install-Package CoreOne.ModelPatch
```

## 🛠️ Usage

### 1. Define Your Entity

```csharp
public class User
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string OtherField { get; set; }
    public Address Location { get; set; }
    public ICollection<Tag> Tags { get; set; }
}

public class Address
{
    public string City { get; set; }
    public string Country { get; set; }
}

[Index(nameof(Value), IsUnique = true)]
public class UserTag
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Value { get; set; }

    public User? User { get; set; }
}
```

### 2. Create a Patch Model

```csharp
var model = new User {
    Name = "John",
    Locaion = new Address {
        City = "New York",
        Country = "USA"
    },
    Tags = new List<Tag> {
        new UserTag { Value = "tag1" } // this record is added
        new UserTag { Value = "tag1" } // this one will be ignored
        new UserTag { Value = "tag2" } // this record is added
    }
};

```

### 3. Apply the Patch

Only fields present in the delta model are updated, if certain fields are not available then that field does not get the update.

```csharp
/* This step should come from API as Delta<T>
 * but for testing purposes, this is how we convert to delta
 */
var delta = Utility.DeserializeObject<Delta<T>>(Utility.Serialize(model))!;
delta.Remove("OtherField");
var dataService = new DataModelService<YourContext>(IServiceProvider instance, yourContextInstance);
var result = await dataService.Patch(delta);
```

This will update the `Name` and `Location.City` properties of the `User` entity.

## 🧪 Sample API Endpoint

```csharp
private readonly DataModelService _dataService;

public UserController(DataModelService dataService) => _dataService = dataService;

[HttpPatch("{id}")]
public async Task<IActionResult> PatchUser(int id, [FromBody] Delta<User> patchData, CancellationToken cancellationToken)
{
    var result = await _dataService.Patch(delta, cancellationToken);
    return Updated(result);
}
```

## 📚 Documentation

The library uses reflection to traverse and update properties. It’s designed to be intuitive and extensible for most EF Core scenarios.

## 🤝 Contributing

Feel free to fork the repo, submit issues, or open pull requests. Contributions are welcome!

## 📄 License

This project is licensed under the MIT License.

---

Would you like help writing unit tests or integrating this into a specific project?
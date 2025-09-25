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
/* This step should come from API as Delta<T>
 * but for testing purposes, this is how we convert to delta
 */
var delta = Utility.DeserializeObject<Delta<T>>(Utility.Serialize(model))!;
```

### 3. Apply the Patch

```csharp

var dataService = new DataModelService<YourContext>(IServiceProvider instance, yourContextInstance);

var result = await dataService.Patch(delta);
```

This will update the `Name` and `Location.City` properties of the `User` entity.

## 🧪 Sample API Endpoint

```csharp
[HttpPatch("{id}")]
public IActionResult PatchUser(int id, [FromBody] Dictionary<string, object> patchData)
{
    var user = _dbContext.Users.Find(id);
    if (user == null) return NotFound();

    ModelPatch.Apply(user, patchData);
    _dbContext.SaveChanges();

    return NoContent();
}
```

## 📚 Documentation

The library uses reflection to traverse and update properties based on dot-separated keys. It’s designed to be intuitive and extensible for most EF Core scenarios.

## 🤝 Contributing

Feel free to fork the repo, submit issues, or open pull requests. Contributions are welcome!

## 📄 License

This project is licensed under the MIT License.

---

Would you like help writing unit tests or integrating this into a specific project?
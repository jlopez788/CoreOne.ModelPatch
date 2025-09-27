using CoreOne.ModelPatch.Test.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CoreOne.ModelPatch.Test.Data;

public class TestDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<Blog> Blogs { get; set; }
    public DbSet<Post> Posts { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        { // Loop through all properties of the entity
            foreach (var property in entityType.ClrType.GetProperties())
            { // Check if the property is an enum
                if (property.PropertyType.IsEnum)
                {
                    modelBuilder.Entity(entityType.ClrType)
                        .Property(property.Name)
                        .HasConversion<string>();
                }

                // Handle Nullable<Enum> (e.g., Status?)
                var underlyingType = Nullable.GetUnderlyingType(property.PropertyType);
                if (underlyingType != null && underlyingType.IsEnum)
                {
                    modelBuilder.Entity(entityType.ClrType)
                        .Property(property.Name)
                        .HasConversion(typeof(EnumToStringConverter<>).MakeGenericType(underlyingType));
                }
            }
        }
    }
}
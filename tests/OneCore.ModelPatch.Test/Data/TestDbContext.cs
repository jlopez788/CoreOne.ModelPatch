using Microsoft.EntityFrameworkCore;
using OneCore.ModelPatch.Tests.Models;

namespace OneCore.ModelPatch.Tests.Data;

public class TestDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<User> Users { get; set; }
    public DbSet<Blog> Blogs { get; set; }
    public DbSet<Post> Posts { get; set; }
}
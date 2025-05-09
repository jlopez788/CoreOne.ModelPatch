using CoreOne.ModelPatch.Test.Models;
using Microsoft.EntityFrameworkCore;

namespace CoreOne.ModelPatch.Test.Data;

public class TestDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<User> Users { get; set; }
    public DbSet<Blog> Blogs { get; set; }
    public DbSet<Post> Posts { get; set; }
}
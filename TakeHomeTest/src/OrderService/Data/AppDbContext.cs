using Microsoft.EntityFrameworkCore;
using OrderServiceAPI.Models;

namespace UserServiceAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<Order> Orders => Set<Order>();
        public DbSet<User> Users => Set<User>();
    }
}

using Authentication_Service.Models;
using Microsoft.EntityFrameworkCore;

namespace Authentication_Service.Data
{
    public class AuthDbContext: DbContext
    {
        public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }
        public DbSet<User> Users { get; set; }
    }
}

using Microsoft.EntityFrameworkCore;

namespace Frontend.Source {
    public enum RoleType {
        //只读
        Viewer = 0,
        //可读可写(增删设备,管理设备信息)
        Operator = 1,
        //可读可写,增删用户
        Admin = 2
    }
    public class User {
        public uint Id { get; set; } = 0;
        public string Username { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public RoleType Role { get; set; } = RoleType.Viewer;
    }
    public class OwnerMap {
        public uint UserId { get; set; }
        public uint Owner { get; set; }
    }
    public class DbCtx : DbContext {
        public DbSet<User> Users => Set<User>();
        public DbSet<OwnerMap> Owners => Set<OwnerMap>();
        public DbCtx(DbContextOptions<DbCtx> options) : base(options) { }
        protected override void OnModelCreating(ModelBuilder modelBuilder) {
            modelBuilder.Entity<User>(e => {
                e.HasKey(e => e.Id);
                e.HasIndex(e => e.Username).IsUnique();
            });
            modelBuilder.Entity<OwnerMap>(e => {
                e.HasKey(k => new { k.Owner, k.UserId });
            });
        }
    }
}

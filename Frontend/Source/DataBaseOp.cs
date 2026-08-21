using MessagePack;
using MessagePack.Resolvers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace Frontend.Source {
    public class DataBaseOp {
        private DbCtx db;
        public DataBaseOp(DbCtx db) {
            this.db = db;
        }
        public async Task<uint> AddUser(string name, string pwd, RoleType role) {
            if (await db.Users.AnyAsync(u => u.Username == name)) throw new Exception($"用户名{name}已存在");
            string hash = BCrypt.Net.BCrypt.HashPassword(pwd);
            var user = new User() {
                Username = name,
                PasswordHash = hash,
                Role = role
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return user.Id;
        }
        public async Task RemoveUser(uint id) {
            using var transaction = await db.Database.BeginTransactionAsync();
            try {
                await db.Owners.Where(o => o.UserId == id).ExecuteDeleteAsync();
                await db.Users.Where(o => o.Id == id).ExecuteDeleteAsync();
                await transaction.CommitAsync();
            } catch (Exception) {
                await transaction.RollbackAsync();
                throw;
            }
        }
        public async Task ModifyRole(uint id, RoleType type) {
            int rowsAffected = await db.Users.Where(u => u.Id == id).ExecuteUpdateAsync(s => s.SetProperty(u => u.Role, type));
            if (rowsAffected == 0) throw new Exception($"用户{id}不存在");
        }
        public async Task<User?> GetUser(uint id) {
            return await db.Users.FindAsync(id);
        }
        public async Task AddMap(uint id, uint owner) {
            if (await db.Owners.AnyAsync(o => o.UserId == id && o.Owner == owner)) return;
            db.Owners.Add(new() { UserId = id, Owner = owner });
            await db.SaveChangesAsync();
        }
        public async Task RemoveMap(uint id, uint owner) {
            await db.Owners.Where(o => o.UserId == id && o.Owner == owner).ExecuteDeleteAsync();
        }
        public async Task<List<uint>> GetMap(uint id) {
            return await db.Owners.Where(o => o.UserId == id).Select(o => o.Owner).ToListAsync();
        }
        public async Task<User?> GetAndVerify(string name, string pwd) {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == name);
            if (user == null) return user;
            return BCrypt.Net.BCrypt.Verify(pwd, user.PasswordHash) ? user : null;
        }
    }
    public interface IDBOp {
        public Task<uint> AddUser(string name, string pwd, RoleType role);
        public Task<User?> GetUser(uint id);
        public Task RemoveUser(uint id);
        public Task ModifyRole(uint id, RoleType type);
        public Task AddMap(uint id, uint owner);
        public Task RemoveMap(uint id, uint owner);
        public Task<List<uint>> GetMap(uint id);
        public Task<User?> GetAndVerify(string name, string pwd);
    }
    public class DBOpWithCache : IDBOp {
        private IDistributedCache _cache;
        private DataBaseOp _db;
        public DBOpWithCache(DataBaseOp dataBaseOp, IDistributedCache cache) {
            _cache = cache;
            _db = dataBaseOp;
        }
        private DistributedCacheEntryOptions GetOptions() => new() {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(Random.Shared.NextDouble() + 5)
        };
        private async Task<T?> Get<T>(string key) where T : class {
            var bytes = await _cache.GetAsync(key);
            return bytes != null ? SerializerTool.Deserialize<T>(bytes) : null;
        }
        private async Task Set<T>(string key, T obj) {
            await _cache.SetAsync(key, SerializerTool.Serialize(obj), GetOptions());
        }
        private async Task<T?> GetOrSet<T>(string key, Func<Task<T?>> fetch) where T : class {
            var obj = await Get<T>(key);
            if (obj != null) return obj;
            var data = await fetch();
            if (data != null) await Set(key, data);
            return data;
        }
        public Task<uint> AddUser(string name, string pwd, RoleType role) => _db.AddUser(name, pwd, role);
        public async Task<User?> GetUser(uint id) => await GetOrSet($"User:{id}", () => _db.GetUser(id));
        public async Task RemoveUser(uint id) {
            await _db.RemoveUser(id);
            await _cache.RemoveAsync($"User:{id}");
            await _cache.RemoveAsync($"Map:{id}");
        }
        public async Task ModifyRole(uint id, RoleType type) {
            await _db.ModifyRole(id, type);
            await _cache.RemoveAsync($"User:{id}");
        }
        public async Task AddMap(uint id, uint owner) {
            await _db.AddMap(id, owner);
            await _cache.RemoveAsync($"Map:{id}");
        }
        public async Task RemoveMap(uint id, uint owner) {
            await _db.RemoveMap(id, owner);
            await _cache.RemoveAsync($"Map:{id}");
        }
        public async Task<List<uint>> GetMap(uint id) => await GetOrSet($"Map:{id}", () => _db.GetMap(id)!) ?? new();
        public Task<User?> GetAndVerify(string name, string pwd) => _db.GetAndVerify(name, pwd);
    }
}

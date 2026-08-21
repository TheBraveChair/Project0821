using StackExchange.Redis;
using System.Text;

namespace Frontend.Source {
    public interface IMachineMap {
        public Task<MachineManager> GetManager();
    }
    public class BackendManager : IMachineMap {
        private readonly IConnectionMultiplexer _cache;
        private readonly string _instanceName;

        public BackendManager(IConnectionMultiplexer cache, Config config) {
            _cache = cache;
            _instanceName = config.RedisInstanceName ?? "";
        }

        public async Task<Dictionary<uint, string>> GetMachineMapAsync() {
            var map = new Dictionary<uint, string>();
            var db = _cache.GetDatabase();
            var endpoints = _cache.GetEndPoints();
            if (endpoints.Length == 0) return map;
            string prefix = $"{_instanceName}Machine:";
            var keys = _cache.GetServer(endpoints[0]).Keys(pattern: $"{_instanceName}Machine:*").ToArray();
            if (keys.Length == 0) return map;
            var tasks = keys.Select(k => db.HashGetAsync(k, "data")).ToArray();
            var results = await Task.WhenAll(tasks);
            for (int i = 0; i < keys.Length; i++) {
                if (results[i].HasValue) {
                    string endpoint = Encoding.UTF8.GetString((byte[])results[i]!);
                    string fullKey = keys[i].ToString();
                    string machineId = fullKey.StartsWith(prefix) ? fullKey.Substring(prefix.Length) : fullKey;
                    map[uint.Parse(machineId)] = endpoint;
                }
            }
            return map;
        }
        public async Task<MachineManager> GetManager() => new(await GetMachineMapAsync());
    }
}
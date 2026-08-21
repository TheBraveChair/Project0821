using Microsoft.Extensions.Caching.Distributed;

namespace ASPNET.Source {
    public class DeviceCacheService : IDeviceService {
        private readonly DeviceDBService _db;
        private readonly IDistributedCache _cache;
        public DeviceCacheService(DeviceDBService dbService, IDistributedCache cache) {
            _db = dbService;
            _cache = cache;
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
        public Task<FieldDefinition?> GetFieldDefinition(FieldDefineHandle handle) =>
            GetOrSet($"FieldDef:{handle.Id}", () => _db.GetFieldDefinition(handle));
        public Task<DeviceType?> GetDeviceType(DeviceTypeHandle handle) =>
            GetOrSet($"DeviceType:{handle.Id}", () => _db.GetDeviceType(handle));
        public Task<DeviceType?> GetDeviceTypeWithFields(DeviceTypeHandle handle) =>
            GetOrSet($"DeviceTypeWithFields:{handle.Id}", () => _db.GetDeviceTypeWithFields(handle));
        public Task<Device?> GetDevice(DeviceHandle handle) =>
            GetOrSet($"Device:{handle.Id}", () => _db.GetDevice(handle));
        public Task<Device?> GetDeviceWithFields(DeviceHandle handle) =>
            GetOrSet($"DeviceWithFields:{handle.Id}", () => _db.GetDeviceWithFields(handle));

        public async Task<FieldDefineHandle> AddFieldDefinition(string name, FieldType type, string unit, string? description = null) =>
            await _db.AddFieldDefinition(name, type, unit, description);
        public async Task RemoveFieldDefinition(FieldDefineHandle handle) {
            await _db.RemoveFieldDefinition(handle);
            await _cache.RemoveAsync($"FieldDef:{handle.Id}");
        }
        public async Task ModifyFieldDefinitionDescription(FieldDefineHandle handle, string description) {
            await _db.ModifyFieldDefinitionDescription(handle, description);
            await _cache.RemoveAsync($"FieldDef:{handle.Id}");
        }
        public async Task<DeviceTypeHandle> AddDeviceType(string name, List<FieldDefineHandle> fields, string? expr, string? description = null) =>
            await _db.AddDeviceType(name, fields, expr, description);
        public async Task RemoveDeviceType(DeviceTypeHandle handle) {
            await _db.RemoveDeviceType(handle);
            await _cache.RemoveAsync($"DeviceType:{handle.Id}");
            await _cache.RemoveAsync($"DeviceTypeWithFields:{handle.Id}");
        }
        public async Task ModifyDeviceTypeDescription(DeviceTypeHandle handle, string description) {
            await _db.ModifyDeviceTypeDescription(handle, description);
            await _cache.RemoveAsync($"DeviceType:{handle.Id}");
        }
        public async Task<DeviceHandle> AddDevice(DeviceTypeHandle handle, string deviceCode, uint owner) =>
            await _db.AddDevice(handle, deviceCode, owner);
        public async Task RemoveDevice(DeviceHandle handle) {
            await _db.RemoveDevice(handle);
            await _cache.RemoveAsync($"Device:{handle.Id}");
            await _cache.RemoveAsync($"DeviceWithFields:{handle.Id}");
        }
        public Task<FieldValueHandle> AddFieldValue(FieldValue value) => _db.AddFieldValue(value);
        public Task AddFieldValues(IEnumerable<FieldValue> values) => _db.AddFieldValues(values);
        public Task<FieldValue?> GetFieldValue(FieldValueHandle handle) => _db.GetFieldValue(handle);
        public Task RemoveFieldValue(FieldValueHandle handle) => _db.RemoveFieldValue(handle);
        public Task<FieldValue?> GetLatestFieldValue(DeviceHandle device, FieldDefineHandle field) => _db.GetLatestFieldValue(device, field);
    }
}
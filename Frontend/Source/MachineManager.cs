using System.Text.Json.Serialization;

namespace Frontend.Source {
    public class OfflineException : Exception {
        public OfflineException(string message) : base(message) { }
    }
    public readonly struct DeviceHandle {
        public long Id { get; init; }
        [JsonIgnore]
        public uint OwnerId => (uint)(Id >> 32);
        [JsonIgnore]
        public uint LocalId => (uint)(Id & 0xFFFFFFFF);
        public DeviceHandle(long id) {
            Id = id;
        }
        public DeviceHandle(uint ownerId, uint localId) {
            Id = ((long)ownerId << 32) | localId;
        }
        public override string ToString() => Id.ToString();
    }
    public class MachineManager {
        private Dictionary<uint, Machine> _machineMap = new();
        private List<uint> _map = new();
        public MachineManager(Dictionary<uint, string> map) {
            foreach (var i in map) {
                var m = new Machine(i.Key, i.Value);
                _machineMap.TryAdd(i.Key, m);
                _map.Add(i.Key);
            }
        }
        public Machine RandomMachine {
            get {
                if (_map.Count != 0) return _machineMap[_map[Random.Shared.Next(_map.Count)]];
                throw new InvalidOperationException("当前没有在线的后端机器");
            }
        }

        public Machine GetTargetMachine(uint ownerId) {
            if (_machineMap.TryGetValue(ownerId, out var machine)) return machine;
            throw new OfflineException($"目标机器 [{ownerId}] 不在线或不存在");
        }
        public bool IsExist(uint ownerId) {
            return _machineMap.ContainsKey(ownerId);
        }
        public List<uint> AllMachinesId=> _machineMap.Keys.ToList();
        public async Task<Handle> RegisterDeviceType(DeviceTypeCreateBody body)//默认共享同一个数据库,如果不共享改为精确路由
            => new((await RandomMachine.OpRegisterDeviceType(body)).Id);
        public async Task<DeviceTypeWithFieldsView?> OpGetDeviceType(Handle handle)
            => await RandomMachine.OpGetDeviceType(handle);
        public async Task OpRemoveDeviceType(Handle handle)
            => await RandomMachine.OpRemoveDeviceType(handle);
        public async Task<DeviceHandle> OpRegisterDevice(uint ownerId, DeviceCreateBody body) {
            var handle = await GetTargetMachine(ownerId).OpRegisterDevice(body);
            return new DeviceHandle(handle.Id);
        }
        public async Task<DeviceTypeView?> OpGetDevice(DeviceHandle handle)
            => await GetTargetMachine(handle.OwnerId).OpGetDevice(new(handle.Id));
        public async Task OpRemoveDevice(DeviceHandle handle)
            => await GetTargetMachine(handle.OwnerId).OpRemoveDevice(new(handle.Id));
    }
}

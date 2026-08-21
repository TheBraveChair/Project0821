using Microsoft.EntityFrameworkCore;

namespace ASPNET.Source {
    public class LocalDeviceIdGenerator {
        private uint _currentLocalId;
        private readonly uint _machineId;
        private bool _isInitialized = false;
        public LocalDeviceIdGenerator(IEnvConfig config) {
            _machineId = config.MachineId;
        }
        public void Init(IServiceProvider provider) {
            if (_isInitialized) return;
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DeviceCtx>();
            long maxId = db.Devices
                .Where(d => d.OwnerId == _machineId).IgnoreQueryFilters()
                .Max(d => (long?)d.Id) ?? 0;
            _currentLocalId = (uint)(maxId & 0xFFFFFFFF);
            _isInitialized = true;
        }

        public long NextId() {
            if (!_isInitialized) throw new InvalidOperationException("ID 生成器未初始化");
            uint nextLocalId = Interlocked.Increment(ref _currentLocalId);
            if (nextLocalId == 0) throw new OverflowException($"[{_machineId}] 的本地 DeviceId 已耗尽");
            return new DeviceHandle(_machineId, nextLocalId).Id;
        }
    }
}
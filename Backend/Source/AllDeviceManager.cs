using MediatR;
using Microsoft.EntityFrameworkCore;
namespace ASPNET.Source {
    public interface IDeviceManager {
        public void Dispatch(ExchangeData data);
        public void AddDevice(DeviceHandle handle, List<InitField> fields, string expr);
        public void RemoveDevice(DeviceHandle handle);
        public List<long> AllDevices();
        public int UploadTime { get; }
        public int ChannelMax { get; }
    }
    public class AllDeviceManager : BackgroundService, IDeviceManager {
        private readonly DeviceManager[] _managers;
        private readonly IMediator _mediator;
        private readonly IServiceProvider _provider;
        private readonly IEnvConfig _config;
        public int UploadTime { get; }
        public int ChannelMax { get; }
        public AllDeviceManager(IMediator mediator, IEnvConfig config, IServiceProvider provider) {
            UploadTime = config.UploadTime;
            ChannelMax = config.ChannelMax;
            _mediator = mediator;
            _provider = provider;
            _config = config;
            int count = Environment.ProcessorCount;
            _managers = new DeviceManager[count];
            for (var i = 0; i < count; ++i) _managers[i] = new(UploadTime, _mediator, ChannelMax / count);
        }
        private int ManagerIndex(long deviceId) => Math.Abs(deviceId.GetHashCode()) % _managers.Length;
        private DeviceManager GetManger(long id) => _managers[ManagerIndex(id)];
        public void Dispatch(ExchangeData data) {
            GetManger(data.Device).EnqueueData(data);
        }
        public void AddDevice(DeviceHandle handle, List<InitField> fields, string expr) {
            GetManger(handle.Id).AddInstance(handle, fields, expr);
        }
        public void RemoveDevice(DeviceHandle handle) {
            GetManger(handle.Id).RemoveInstance(handle);
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            await InitDevices();
            foreach (var i in _managers) i.Start(stoppingToken);
            return;
        }
        private async Task InitDevices() {
            using var sp = _provider.CreateScope();
            var dbobj = sp.ServiceProvider.GetService(typeof(DeviceCtx));
            if (dbobj == null) throw new Exception("No Db");
            var db = (DeviceCtx)dbobj;
            uint mid = _config.MachineId;
            var devices = db.Devices.Include(c => c.DeviceType).ThenInclude(t => t.Fields).Where(d => d.OwnerId == mid && !d.IsDeleted).AsEnumerable();
            foreach (var device in devices) {
                var initFields = new List<InitField>();
                foreach (var field in device.DeviceType.Fields) {
                    initFields.Add(new InitField(
                        Type: field.Type,
                        DecimalValue: null,
                        StringValue: null,
                        Name: field.Name,
                        FieldId: field.Id
                    ));
                }
                string? expr = device.DeviceType.VerifyExpression;
                if (expr == null || expr.Length == 0) expr = "true";
                AddDevice(new(device.Id), initFields, expr);
            }
        }
        public override Task StopAsync(CancellationToken cancellationToken) {
            foreach (var i in _managers) i.Stop();
            return Task.CompletedTask;
        }
        public List<long> AllDevices() {
            var list = new List<long>();
            foreach (var i in _managers) list.AddRange(i.Instances);
            return list;
        }
    }
}

using MediatR;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace ASPNET.Source {
    public struct ExchangeData {//设备发送来的数据的中间表示
        public record struct FieldData(string Field, decimal Value);
        public long Device { get; }
        public List<FieldData> Data { get; }
        public ExchangeData(long device, List<FieldData> data) {
            Device = device;
            Data = data;
        }
    }
    public class DeviceManager {
        private ConcurrentDictionary<long, DeviceInstance> deviceInstances = new();
        private readonly IMediator _mediator;
        private readonly Channel<ExchangeData> _channel;
        private bool used = false;
        private int uploadOffset;
        public List<long> Instances => deviceInstances.Keys.ToList();
        public DeviceManager(int uploadOffset, IMediator mediator, int channelMax) {
            this.uploadOffset = uploadOffset;
            this._mediator = mediator;
            this._channel = Channel.CreateBounded<ExchangeData>(
            new BoundedChannelOptions(channelMax) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait }
        );
        }
        public void AddInstance(DeviceHandle handle, List<InitField> fields, string expr) {
            var instance = new DeviceInstance(handle);
            instance.InitExpr(expr);
            instance.UpdateConfig(fields);
            deviceInstances[handle.Id] = instance;
        }
        public DeviceInstance? RemoveInstance(DeviceHandle handle) {
            if (deviceInstances.Remove(handle.Id, out var value)) return value;
            return null;
        }
        public void EnqueueData(ExchangeData data) {
            _channel.Writer.TryWrite(data);
        }
        public void Stop() {
            _channel.Writer.Complete();
            used = false;
        }
        public void Start(CancellationToken token) {
            if (used) return;
            used = true;
            Task.Run(async () => {
                try {
                    await foreach (var data in _channel.Reader.ReadAllAsync(token)) {
                        try {
                            ProcessData(data);
                        } catch (Exception ex) {
                            _ = _mediator.Publish(new LogEvent($"设备<{data.Device}>异常:{ex.ToString()}", LogLevel.Error), token);
                        }
                    }
                } catch (OperationCanceledException) { }
            }, token);
        }
        private void ProcessData(ExchangeData data) {
            if (deviceInstances.TryGetValue(data.Device, out var device)) {
                foreach (var i in data.Data) device.UpdateValue(i.Field, i.Value);
                UploadData(device, !device.IsTrue());
            } else {
                throw new Exception($"配置未初始化");
            }
        }
        private void UploadData(DeviceInstance device, bool error) {
            //这里不加 !error&& 是因为如果5s内设备自己脱离报警区间那就没必要了
            if ((DateTimeOffset.UtcNow - device.LastUpdateTime).TotalMilliseconds < uploadOffset) return;
            device.LastUpdateTime = DateTimeOffset.UtcNow;
            _ = _mediator.Publish(new UploadDataEvent(device.Device, device.GetData(), error, device.LastUpdateTime));
        }
    }
}
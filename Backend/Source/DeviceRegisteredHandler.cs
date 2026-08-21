using MediatR;

namespace ASPNET.Source {
    public class DeviceRegisteredHandler : INotificationHandler<DeviceRegisteredEvent> {
        private readonly IDeviceManager _deviceManager;
        private readonly IDeviceService _deviceService;
        private readonly IMediator _mediator;
        public DeviceRegisteredHandler(IDeviceManager deviceManager, IDeviceService deviceService, IMediator mediator) {
            _deviceManager = deviceManager;
            _deviceService = deviceService;
            _mediator = mediator;
        }
        public async Task Handle(DeviceRegisteredEvent notification, CancellationToken cancellationToken) {
            try {
                var device = await _deviceService.GetDeviceWithFields(notification.Handle);
                if (device == null || device.DeviceType == null) {
                    _ = _mediator.Publish(new LogEvent($"设备注册事件处理失败: 找不到设备 <{notification.Handle.Id}>", LogLevel.Warning), cancellationToken);
                    return;
                }
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
                _deviceManager.AddDevice(notification.Handle, initFields, expr);
                _ = _mediator.Publish(new LogEvent($"设备 <{notification.Handle.Id}> 已成功加载到内存管理器", LogLevel.Information), cancellationToken);
            } catch (Exception ex) {
                _ = _mediator.Publish(new LogEvent($"处理设备注册事件时发生异常: {ex.Message}", LogLevel.Error), cancellationToken);
            }
        }
    }
}
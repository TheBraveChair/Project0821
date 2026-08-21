using MediatR;

namespace ASPNET.Source {
    public class DeviceRemoveHandler : INotificationHandler<DeviceRemoveEvent> {
        private readonly IDeviceManager _deviceManager;
        private readonly IMediator _mediator;
        public DeviceRemoveHandler(IDeviceManager manager, IMediator mediator) {
            _deviceManager = manager;
            _mediator = mediator;
        }
        public async Task Handle(DeviceRemoveEvent notification, CancellationToken cancellationToken) {
            var hd = notification.Handle;
            _deviceManager.RemoveDevice(hd);
            _ = _mediator.Publish(new LogEvent($"设备<{hd.Id}>已删除.", LogLevel.Information), cancellationToken);
        }
    }
}

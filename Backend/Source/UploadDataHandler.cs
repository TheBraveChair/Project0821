using MediatR;
using System.Collections.Concurrent;

namespace ASPNET.Source {
    public class UploadDataHandler : INotificationHandler<UploadDataBody> {
        private readonly IDeviceManager _manager;
        private static readonly ConcurrentDictionary<long, DateTimeOffset> time = new();
        public UploadDataHandler(IDeviceManager manager) {
            _manager = manager;
        }
        public async Task Handle(UploadDataBody notification, CancellationToken cancellationToken) {
            if (!time.TryGetValue(notification.Device, out var t)) t = notification.Time;
            if (notification.Time < t) return;
            time[notification.Device] = notification.Time;
            _manager.Dispatch(new(notification.Device, notification.Data));
        }
    }
}

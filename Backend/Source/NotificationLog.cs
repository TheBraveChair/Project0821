using MediatR;

namespace ASPNET.Source {
    public class NotificationLog : INotificationHandler<HandleLogEvent> {
        public async Task Handle(HandleLogEvent notification, CancellationToken cancellationToken) {
            if (notification.Level == LogLevel.Critical) {
                //发消息,通知什么的
            }
        }
    }
}

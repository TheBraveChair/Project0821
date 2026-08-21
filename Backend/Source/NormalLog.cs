using MediatR;

namespace ASPNET.Source {
    public class NormalLog : INotificationHandler<HandleLogEvent> {
        private readonly ILogger<NormalLog> _logger;
        public NormalLog(ILogger<NormalLog> logger) {
            _logger = logger;
        }
        public Task Handle(HandleLogEvent notification, CancellationToken cancellationToken) {
            _logger.Log(notification.Level, notification.Message);
            return Task.CompletedTask;
        }
    }
}

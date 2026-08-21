using MediatR;

namespace ASPNET.Source {
    public record class HandleLogEvent(string Message, LogLevel Level) : INotification;
    public class LogService : BackgroundService {
        private LogChannel _channel;
        private IMediator _mediator;
        public LogService(LogChannel channel, IMediator mediator) {
            _channel = channel;
            _mediator = mediator;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            await foreach (var ev in _channel.Channel.Reader.ReadAllAsync(stoppingToken)) {
                if (ev != null) {
                    await _mediator.Publish(new HandleLogEvent(ev.Message, ev.Level));
                }
            }
        }
    }
}

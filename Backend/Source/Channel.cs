using ASPNET.Source.Expression;
using MediatR;
using System.Threading.Channels;

namespace ASPNET.Source {
    public record class UploadDataEvent(DeviceHandle Handle, List<FieldValueStruct> Values, bool IsError,DateTimeOffset Time) : INotification;
    public record class LogEvent(string Message, LogLevel Level = LogLevel.Information) : INotification;
    public class LogChannel {
        public Channel<LogEvent> Channel { get; } =
            System.Threading.Channels.Channel.CreateUnbounded<LogEvent>(
                new UnboundedChannelOptions() { SingleReader = true }
                );
    }
    public class UploadDataChannel {
        public Channel<UploadDataEvent> Channel { get; } =
            System.Threading.Channels.Channel.CreateUnbounded<UploadDataEvent>(
                new UnboundedChannelOptions() { SingleReader = true }
                );
    }
    public class UploadDataEventHandler : INotificationHandler<UploadDataEvent> {
        private readonly UploadDataChannel _channel;
        public UploadDataEventHandler(UploadDataChannel channel) {
            _channel = channel;
        }
        public Task Handle(UploadDataEvent notification, CancellationToken cancellationToken) {
            _channel.Channel.Writer.TryWrite(notification);
            return Task.CompletedTask;
        }
    }
    public class LogEventHandler : INotificationHandler<LogEvent> {
        private readonly LogChannel _channel;
        public LogEventHandler(LogChannel channel) {
            _channel = channel;
        }
        public Task Handle(LogEvent notification, CancellationToken cancellationToken) {
            _channel.Channel.Writer.TryWrite(notification);
            return Task.CompletedTask;
        }
    }
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace ASPNET.Source {
    public class UploadDataService : BackgroundService {
        private UploadDataChannel _channel;
        private IServiceProvider _serviceProvider;
        private IMediator _mediator;
        private IEnvConfig _config;
        public UploadDataService(UploadDataChannel channel, IServiceProvider serviceProvider, IMediator mediator, IEnvConfig config) {
            _channel = channel;
            _mediator = mediator;
            _serviceProvider = serviceProvider;
            _config = config;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            var batch = new List<UploadDataEvent>();
            while (!stoppingToken.IsCancellationRequested) {
                try {
                    var first = await _channel.Channel.Reader.ReadAsync(stoppingToken);
                    batch.Add(first);
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    cts.CancelAfter(TimeSpan.FromSeconds(_config.MaxWaitTime));
                    try {
                        var c = 0;
                        while (c < _config.MaxBatchSize) {
                            batch.Add(await _channel.Channel.Reader.ReadAsync(cts.Token));
                            ++c;
                        }
                    } catch (OperationCanceledException) { }
                    if (batch.Count > 0) {
                        await ProcessBatchAsync(batch, stoppingToken);
                        batch.Clear();
                    }
                } catch (OperationCanceledException) {
                    break;
                } catch (Exception ex) {
                    _ = _mediator.Publish(new LogEvent($"读取 Channel 发生错误 {ex}", LogLevel.Error));
                }
            }
        }
        private async Task ProcessBatchAsync(List<UploadDataEvent> batch, CancellationToken token) {
            using var scope = _serviceProvider.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IDeviceService>();
            var db = scope.ServiceProvider.GetRequiredService<DeviceCtx>();
            var insert = new List<FieldValue>();
            var errorMessage = new StringBuilder();
            var uploadWarn = new List<Warn>();
            foreach (var ev in batch) {
                try {
                    if (ev.Values.Count == 0) continue;
                    foreach (var val in ev.Values) {
                        insert.Add(new FieldValue(0, ev.Handle.Id, val.FieldId, ev.Time) {
                            DecimalValue = val.DValue,
                            StringValue = val.SValue
                        });
                    }
                    if (ev.IsError) {
                        uploadWarn.Add(new() { DeviceId = ev.Handle.Id, Time = ev.Time });
                        errorMessage.AppendLine($"设备 {ev.Handle.Id} 触发报警");
                    }
                } catch (Exception e) {
                    _ = _mediator.Publish(new LogEvent($"解析设备<{ev.Handle.Id}>数据时发生异常 {e}", LogLevel.Error), token);
                }
            }
            if (insert.Count > 0) {
                try {
                    await service.AddFieldValues(insert);
                    await WarnHandle(db, uploadWarn, token);
                    await db.SaveChangesAsync(token);
                } catch (Exception e) {
                    _ = _mediator.Publish(new LogEvent($"批量保存 {insert.Count} 条数据时发生异常 {e}", LogLevel.Error), token);
                }
            }
            if (errorMessage.Length > 0) {
                _ = _mediator.Publish(new LogEvent(errorMessage.ToString(), LogLevel.Warning), token);
            }
        }
        private async Task WarnHandle(DeviceCtx db, List<Warn> warnings, CancellationToken token) {//警告的表通常数据非常少
            if (warnings.Count == 0) return;
            List<Warn> lastest = warnings.GroupBy(w => w.DeviceId).Select(g => g.MaxBy(w => w.Time)).ToList()!;
            var ids = lastest.Select(w => w.DeviceId).ToList();
            var exists = await db.WarnTable.Where(w => ids.Contains(w.DeviceId)).ToDictionaryAsync(e => e.DeviceId);
            foreach (var i in lastest) {//这里永远只修改自己管理的设备不会造成并发竞争
                if (exists.TryGetValue(i.DeviceId, out var value)) {
                    if (value.Time < i.Time) value.Time = i.Time;
                } else {
                    db.WarnTable.Add(new() {
                        DeviceId = i.DeviceId,
                        Time = i.Time,
                    });
                }
            }
        }
    }
}
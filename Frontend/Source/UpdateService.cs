namespace Frontend.Source {
    public class MachineManagerProvider {
        private volatile MachineManager? _currentManager;
        public MachineManager? Current => _currentManager;
        public void UpdateManager(MachineManager newManager) {
            _currentManager = newManager;
        }
    }
    //暂时先用轮询,设备数多了可以用消息队列,让Backend直接投递到这里绕过redis
    public class UpdateService : BackgroundService {
        private readonly IServiceProvider _provider;
        private readonly ILogger<UpdateService> _logger;
        private readonly MachineManagerProvider _machineManager;
        public UpdateService(IServiceProvider provider, ILogger<UpdateService> logger, MachineManagerProvider managerProvider) {
            _provider = provider;
            _logger = logger;
            _machineManager = managerProvider;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            while (!stoppingToken.IsCancellationRequested) {
                try {
                    using var sp = _provider.CreateScope();
                    var map = sp.ServiceProvider.GetRequiredService<IMachineMap>();
                    _machineManager.UpdateManager(await map.GetManager());
                } catch (Exception ex) {
                    _logger.LogWarning(ex, "更新机器列表失败，将继续使用旧列表");
                }
                try {
                    await Task.Delay(5000, stoppingToken);
                } catch (TaskCanceledException) {
                    break;
                }
            }
        }
    }
}
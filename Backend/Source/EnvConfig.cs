using System.Text.Json;

namespace ASPNET.Source {
    public interface IEnvConfig {
        public uint MachineId { get; }
        public int UploadTime { get; }
        public int ChannelMax { get; }
        public int MaxBatchSize { get; }
        public int MaxWaitTime { get; }
        public string ConnectionString { get; }
        public string RedisConfiguration { get; }
        public string RedisInstanceName { get; }
    }

    public class EnvConfig : IEnvConfig {
        private readonly uint _machineId = 0;
        private readonly int _uploadTime = 5000;
        private readonly int _channelMax = 5000;
        private readonly int _maxBatchSize = 1000;
        private readonly int _maxWaitTime = 1;
        //测试用的连接字符串
        private readonly string _connectionString = "Server=localhost;Database=DeviceDB;User Id=sa;Password=Root@123456;TrustServerCertificate=True;";
        private readonly string _redisConfiguration = "localhost:6379";
        private readonly string _redisInstanceName = "Cache_";
        private readonly int _port = 5000;
        private record class ConfigData(
            uint MachineId,
            int UploadTime,
            int ChannelMax,
            int MaxBatchSize,
            int MaxWaitTime,
            int Port,
            string? ConnectionString,
            string? RedisConfiguration,
            string? RedisInstanceName
        );

        public EnvConfig(string configName = "config.json") {
            try {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configName);
                var options = new JsonSerializerOptions {
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = true
                };

                if (File.Exists(path)) {
                    string jsonString = File.ReadAllText(path);
                    var config = JsonSerializer.Deserialize<ConfigData>(jsonString, options);
                    if (config != null) {
                        _machineId = config.MachineId;
                        _uploadTime = config.UploadTime;
                        _channelMax = config.ChannelMax;
                        _maxBatchSize = config.MaxBatchSize;
                        _maxWaitTime = config.MaxWaitTime;
                        _port = config.Port;
                        if (!string.IsNullOrWhiteSpace(config.ConnectionString)) _connectionString = config.ConnectionString;
                        if (!string.IsNullOrWhiteSpace(config.RedisConfiguration)) _redisConfiguration = config.RedisConfiguration;
                        if (!string.IsNullOrWhiteSpace(config.RedisInstanceName)) _redisInstanceName = config.RedisInstanceName;
                    }
                } else {
                    File.WriteAllText(path, JsonSerializer.Serialize(new ConfigData(
                        _machineId, _uploadTime, _channelMax, _maxBatchSize, _maxWaitTime, _port,
                        _connectionString, _redisConfiguration, _redisInstanceName
                    ), options));
                    Console.WriteLine($"[Warning] 配置文件 {path} 不存在，已生成默认配置，请填写 ConnectionString 中的密码。");
                }
            } catch (Exception ex) {
                Console.WriteLine($"[Error] 读取配置文件失败: {ex.Message}，将使用默认配置。");
            }
        }

        public uint MachineId => _machineId;
        public int UploadTime => _uploadTime;
        public int ChannelMax => _channelMax;
        public int MaxBatchSize => _maxBatchSize;
        public int MaxWaitTime => _maxWaitTime;
        public string ConnectionString => _connectionString;
        public string RedisConfiguration => _redisConfiguration;
        public string RedisInstanceName => _redisInstanceName;
        public int Port => _port;
    }
}

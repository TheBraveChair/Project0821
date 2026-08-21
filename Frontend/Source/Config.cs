using System.Text.Json;

namespace Frontend.Source {
    public class Config {
        public string RedisConnectionString { get; set; } = "localhost:6379";
        public string RedisInstanceName { get; set; } = "Cache_";
        public string JwtIssuer { get; set; } = "FrontendGateway";
        public string JwtAudience { get; set; } = "FrontendUsers";
        public string ConnectionString { get; set; } = "Server=localhost;Database=UserDB;User Id=sa;Password=Root@123456;TrustServerCertificate=True;";
        public int JwtExpireMinutes { get; set; } = 10;
        public static Config GetConfig(string path = "config.json") {
            if (!File.Exists(path)) {
                File.WriteAllText(path, JsonSerializer.Serialize(new Config()));
                return new();
            }
            var text = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Config>(text) ?? new();
        }
    }
}

using StackExchange.Redis;
using System.Text;
using System.Text.Json;
namespace UploadProgram {
    public class Config {
        public string RedisConnectionString { get; set; } = "localhost:6379";
        public string RedisInstanceName { get; set; } = "Cache_";
        public uint MachineId { get; set; } = 0;
        public long DeviceId { get; set; } = 0;
        public static Config GetConfig(string path = "config.json") {
            if (!File.Exists(path)) {
                File.WriteAllText(path, JsonSerializer.Serialize(new Config()));
                return new();
            }
            var text = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Config>(text) ?? new();
        }
    }
    internal class Program {
        static long device = 0;
        static async Task Main(string[] args) {
            string? address;
            {
                var config = Config.GetConfig();
                device = config.DeviceId;
                using var redis = await ConnectionMultiplexer.ConnectAsync(config.RedisConnectionString);
                var db = redis.GetDatabase();
                string key = $"{config.RedisInstanceName}Machine:{config.MachineId}";
                while (true) {
                    try {
                        var value = await db.HashGetAsync(key, "data");
                        if (value.HasValue) {
                            address = value;
                            break;
                        } else {
                            Console.WriteLine("等待中...");
                            await Task.Delay(5000);
                        }
                    } catch (Exception e) {
                        Console.WriteLine(e);
                        await Task.Delay(5000);
                    }
                }
            }
            string apiUrl = $"http://{address}/api/DeviceOps/UploadData";
            var client = new HttpClient();
            var post = async (string data) => {
                using var ctx = new StringContent(data, Encoding.UTF8, "application/json");
                await client.PostAsync(apiUrl, ctx);
            };
            await Loop(async (List<FieldValue> fields) => await post(AsJson(fields)));
        }
        public class FieldValue {
            public string Field { get; set; }
            public decimal Value { get; set; }
            public FieldValue(string field, decimal value) {
                Field = field;
                Value = value;
            }
        }
        public class PostData {
            public long Device { get; set; } = 0;
            public List<FieldValue> Data { get; set; } = new();
            public DateTimeOffset Time { get; set; }
        }
        public static string AsJson(List<FieldValue> fields) {
            var data = new PostData() { Time = DateTimeOffset.Now, Device = device, Data = fields };
            return JsonSerializer.Serialize(data);
        }
        public static async Task Loop(Func<List<FieldValue>, Task> post) {
            while (true) {
                try {
                    await post(await GetData());
                } catch (Exception ex) {
                    Console.WriteLine(ex);
                }
            }
        }
        public static async Task<List<FieldValue>> GetData() {//测试用的数据
            await Task.Delay(1000);
            var list = new List<FieldValue>();
            if (Random.Shared.Next(2) == 1) {
                list.Add(new("temp", 99m));
                list.Add(new("weight", 1m));
            } else {
                list.Add(new("temp", 80m));
                list.Add(new("weight", 60m));
            }
            return list;
        }
    }
}

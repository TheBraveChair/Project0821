using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Caching.Distributed;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ASPNET.Source {
    public static class MachineRegistry {
        private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);
        public static void Start(WebApplication app) {
            _ = Task.Run(() => Loop(app));
        }
        private static async Task Loop(WebApplication app) {
            var token = app.Lifetime.ApplicationStopping;
            var logger = app.Services.GetRequiredService<ILogger<IEnvConfig>>();
            try {
                var cache = app.Services.GetRequiredService<IDistributedCache>();
                var config = app.Services.GetRequiredService<IEnvConfig>();
                var endpoint = ResolveEndpoint(app);
                if (endpoint == null) {
                    logger.LogError("无法确定本机监听地址,跳过服务注册");
                    return;
                }
                var key = $"Machine:{config.MachineId}";
                var value = Encoding.UTF8.GetBytes(endpoint);
                var options = new DistributedCacheEntryOptions {
                    AbsoluteExpirationRelativeToNow = Ttl
                };
                logger.LogInformation("服务注册 {Key} -> {Endpoint}", key, endpoint);
                using var timer = new PeriodicTimer(Interval);
                do {
                    try {
                        await cache.SetAsync(key, value, options, token);
                    } catch (OperationCanceledException) {
                        throw;
                    } catch (Exception ex) {
                        logger.LogWarning("服务注册心跳失败: {Message}", ex.Message);
                    }
                } while (await timer.WaitForNextTickAsync(token));
            } catch (OperationCanceledException) {
            } catch (Exception ex) {
                logger.LogError(ex, "服务注册后台任务异常退出");
            }
        }

        private static string? ResolveEndpoint(WebApplication app) {
            var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses;
            if (addresses == null || addresses.Count == 0) return null;
            int? port = null;
            foreach (var address in addresses) {
                if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)) continue;
                if (uri.Scheme == Uri.UriSchemeHttp) { port = uri.Port; break; }
                port ??= uri.Port;
            }
            if (port == null) return null;
            var ip = GetLocalIp();
            return ip == null ? null : $"{ip}:{port}";
        }
        private static string? GetLocalIp() {
            try {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.Connect("8.8.8.8", 65530);
                return (socket.LocalEndPoint as IPEndPoint)?.Address.ToString();
            } catch {
                return null;
            }
        }
    }
}

using ASPNET.Source;
using Microsoft.EntityFrameworkCore;

namespace ASPNET {
    public class Program {
        public static void Main(string[] args) {
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddControllers();
            builder.Services.AddOpenApi();
            var envConfig = new EnvConfig();
            builder.Services.AddSingleton<IEnvConfig>(envConfig);
            builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
            builder.Services.AddStackExchangeRedisCache(options => {
                options.Configuration = envConfig.RedisConfiguration;
                options.InstanceName = envConfig.RedisInstanceName;
            });
            builder.Services.AddSingleton<UploadDataChannel>();
            builder.Services.AddSingleton<LogChannel>();
            builder.Services.AddScoped<DeviceDBService>();
            builder.Services.AddDbContext<DeviceCtx>(options => options.UseSqlServer(envConfig.ConnectionString));
            builder.Services.AddHostedService<UploadDataService>();
            builder.Services.AddScoped<IDeviceService, DeviceCacheService>();
            builder.Services.AddSingleton<IDeviceManager, AllDeviceManager>();
            builder.Services.AddHostedService(provider => (AllDeviceManager)provider.GetRequiredService<IDeviceManager>());
            builder.Services.AddHostedService<LogService>();
            builder.Services.AddSingleton<LocalDeviceIdGenerator>();
            builder.WebHost.UseUrls($"http://*:{envConfig.Port}");
            var app = builder.Build();
            if (app.Environment.IsDevelopment()) {
                app.MapOpenApi();
                app.UseSwaggerUI(options => {
                    options.SwaggerEndpoint("/openapi/v1.json", "v1 API");
                });
            }
            app.UseExceptionHandler(errorApp => {
                errorApp.Run(async context => {
                    context.Response.StatusCode = 400;
                    context.Response.ContentType = "application/json";
                    var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>()?.Error;
                    await context.Response.WriteAsJsonAsync(new {
                        message = exception?.Message ?? "后端发生未知错误"
                    });
                });
            });
            app.UseAuthorization();
            app.MapControllers();
            app.Lifetime.ApplicationStarted.Register(() => MachineRegistry.Start(app));//如果出现相同MachineID会自动转发到这里
            app.Services.GetRequiredService<LocalDeviceIdGenerator>().Init(app.Services);//初始化全局自增ID
            app.Run();
        }
    }
}
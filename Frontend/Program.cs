
using Frontend.Source;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Security.Cryptography;

namespace Frontend {
    public class Program {
        public record class JwtKey(byte[] JwtKeys);
        public static async Task Main(string[] args) {
            var cfg = Config.GetConfig();
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddControllers();
            builder.Services.AddSingleton(cfg);
            byte[] jwtKey;
            {
                using var redis = ConnectionMultiplexer.Connect(cfg.RedisConnectionString);
                var db = redis.GetDatabase();
                var key = $"{cfg.RedisInstanceName}GlobalJwtSecret";
                var value = db.StringGet(key);
                if (value.HasValue) {
                    jwtKey = value!;
                } else {
                    jwtKey = new byte[32];
                    RandomNumberGenerator.Fill(jwtKey);
                    if (!db.StringSet(key, jwtKey, when: When.NotExists)) jwtKey = db.StringGet(key)!;
                }
            }
            builder.Services.AddCors(options => {
                options.AddPolicy("AllowAll", policy => {
                    policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
                });
            });
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options => {
                options.TokenValidationParameters = new TokenValidationParameters {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = cfg.JwtIssuer,
                    ValidAudience = cfg.JwtAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(jwtKey)
                };
            });

            builder.Services.AddDbContext<DbCtx>(op => {
                op.UseSqlServer(cfg.ConnectionString);
            });
            builder.Services.AddScoped<DataBaseOp>();
            builder.Services.AddScoped<IDBOp, DBOpWithCache>();
            builder.Services.AddSingleton(new JwtKey(jwtKey));
            builder.Services.AddScoped<IMachineMap, BackendManager>();
            builder.Services.AddHostedService<UpdateService>();
            builder.Services.AddSingleton<MachineManagerProvider>();
            builder.Services.AddOpenApi();
            builder.Services.AddSingleton<JwtGenerater>();
            builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(cfg.RedisConnectionString));
            builder.Services.AddStackExchangeRedisCache(options => {
                options.Configuration = cfg.RedisConnectionString;
                options.InstanceName = cfg.RedisInstanceName;
            });
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
                    await context.Response.WriteAsJsonAsync(new {
                        message = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>()?.Error?.Message ?? "发生未知错误"
                    });
                });
            });
            app.UseHttpsRedirection();
            app.UseCors("AllowAll");
            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            app.MapFallbackToFile("index.html");
            await Task.Run(async () => {
                using (var scope = app.Services.CreateScope()) {
                    var services = scope.ServiceProvider;
                    try {
                        var db = services.GetRequiredService<DbCtx>();
                        await db.Database.EnsureCreatedAsync();
                        if (await db.Users.AnyAsync()) return;
                        string pwd = PasswordGenerator.Generate();
                        await services.GetRequiredService<DataBaseOp>().AddUser("admin", pwd, RoleType.Admin);
                        Console.WriteLine($"默认账号:admin\n{pwd}");
                    } catch (Exception ex) {
                        Console.WriteLine($"[Error] 初始化默认管理员失败: {ex.Message}");
                    }
                }
            });
            app.Run();
        }
    }
}
//TODO:前端页面,报警的查询和确认
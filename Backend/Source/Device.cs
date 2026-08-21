using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
namespace ASPNET.Source {
    public enum FieldType {
        String = 1,
        Decimal = 2
    }

    public sealed class FieldDefinition {//类型描述,可以多个设备类型共享
        public long Id { get; internal set; }
        public string Name { get; private set; } = null!;//实际代码里用的变量名
        public FieldType Type { get; private set; }
        public string? Description { get; set; }
        public string? Unit { get; set; }
        private FieldDefinition() { }
        public FieldDefinition(long id, string name, FieldType type, string unit) {
            this.Id = id;
            this.Name = name;
            this.Type = type;
            this.Unit = unit;
        }
    }

    public sealed class FieldValue {
        public long Id { get; private set; }
        public long DeviceId { get; private set; }//所属实例
        public long FieldDefinitionId { get; private set; }//类型定义
        public decimal? DecimalValue { get; set; }
        public string? StringValue { get; set; }
        public DateTimeOffset Time { get; set; }
        private FieldValue() { }
        public FieldValue(long id, long deviceId, long fieldDefinitionId, DateTimeOffset? time = null) {
            Id = id;
            DeviceId = deviceId;
            FieldDefinitionId = fieldDefinitionId;
            Time = time ?? DateTimeOffset.UtcNow;
        }
        public override string ToString() {
            return $"Id:{Id},DeviceId:{DeviceId},FieldDefinitionId:{FieldDefinitionId},DecimalValue:{DecimalValue},StringValue:{StringValue},Time:{Time}";
        }
    }

    public sealed class DeviceType {
        public long Id { get; private set; }
        public string Name { get; private set; } = null!;//设备码或者别的什么
        public string? VerifyExpression { get; set; }//共享的验证表达式
        public string? Description { get; set; }
        public List<FieldDefinition> Fields { get; private set; } = [];
        private DeviceType() { }
        public DeviceType(long id, string name, List<FieldDefinition> fields, string? verifyExpression = null) {
            Id = id;
            Name = name;
            this.VerifyExpression = verifyExpression;
            Fields = fields;
        }
    }

    public sealed class Device {
        public long Id { get; private set; }
        public long DeviceTypeId { get; private set; }
        public uint OwnerId { get; private set; }//所属的管理设备
        public string DeviceCode { get; private set; } = null!;//具体编码
        public bool IsDeleted { get; private set; } = false;
        public DeviceType DeviceType { get; private set; } = null!;
        private Device() { }
        public Device(long deviceTypeId, uint ownerId, string deviceCode) {
            DeviceTypeId = deviceTypeId;
            OwnerId = ownerId;
            DeviceCode = deviceCode;
        }
    }
    public sealed class Warn {
        public long DeviceId { get; set; }
        public DateTimeOffset Time { get; set; }
    }
    public class DeviceCtx : DbContext {
        public DbSet<Device> Devices => Set<Device>();
        public DbSet<DeviceType> DeviceTypes => Set<DeviceType>();
        public DbSet<FieldDefinition> FieldDefinitions => Set<FieldDefinition>();
        public DbSet<FieldValue> FieldValues => Set<FieldValue>();
        public DbSet<Warn> WarnTable => Set<Warn>();
        public LocalDeviceIdGenerator IdGenerator { get; }
        public DeviceCtx(DbContextOptions<DeviceCtx> options, LocalDeviceIdGenerator idGenerator) : base(options) {
            IdGenerator = idGenerator;
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder) {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Warn>(entity => {
                entity.HasKey(e => e.DeviceId);
                entity.Property(e => e.DeviceId).ValueGeneratedNever();
                entity.HasIndex(w => w.Time);
            });
            modelBuilder.Entity<DeviceType>(entity => {
                entity.HasKey(deviceType => deviceType.Id);
                entity.HasMany(deviceType => deviceType.Fields)
                    .WithMany()
                    .UsingEntity("DeviceTypeFields");
            });
            modelBuilder.Entity<Device>(entity => {
                entity.HasKey(device => device.Id);
                entity.Property(d => d.Id).HasValueGenerator<DeviceIdValueGenerator>().ValueGeneratedOnAdd().HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.None);
                entity.HasOne(device => device.DeviceType).WithMany().HasForeignKey(device => device.DeviceTypeId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(device => device.DeviceCode).IsUnique().HasFilter("[IsDeleted] = 0");
                entity.HasQueryFilter(d => !d.IsDeleted);
                entity.HasIndex(d => d.OwnerId);
            });
            modelBuilder.Entity<FieldValue>(entity => {
                entity.HasKey(value => value.Id);
                entity.HasOne<Device>()
                    .WithMany()
                    .HasForeignKey(value => value.DeviceId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne<FieldDefinition>()
                    .WithMany()
                    .HasForeignKey(value => value.FieldDefinitionId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(value => new {
                    value.DeviceId,
                    value.FieldDefinitionId,
                    value.Time
                });
                entity.Property(f => f.DecimalValue)
       .HasPrecision(18, 4);
            });
            modelBuilder.Entity<FieldDefinition>(entity => {
                entity.HasKey(field => field.Id);
            });
        }
    }
}

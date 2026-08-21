using MessagePack;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

namespace ASPNET.Source {
    public record struct FieldDefineHandle(long Id);
    public record struct DeviceTypeHandle(long Id);
    public readonly struct DeviceHandle {
        public long Id { get; init; }
        [JsonIgnore]
        [IgnoreMember]
        public uint OwnerId => (uint)(Id >> 32);
        [JsonIgnore]
        [IgnoreMember]
        public uint LocalId => (uint)(Id & 0xFFFFFFFF);
        public DeviceHandle(long id) {
            Id = id;
        }
        public DeviceHandle(uint ownerId, uint localId) {
            Id = ((long)ownerId << 32) | localId;
        }
        public override string ToString() => Id.ToString();
    }
    public record struct FieldValueHandle(long Id);

    public interface IDeviceService {
        public Task<FieldDefineHandle> AddFieldDefinition(string name, FieldType type, string unit, string? description = null);
        public Task<FieldDefinition?> GetFieldDefinition(FieldDefineHandle handle);
        public Task RemoveFieldDefinition(FieldDefineHandle handle);
        public Task ModifyFieldDefinitionDescription(FieldDefineHandle handle, string description);
        public Task<DeviceTypeHandle> AddDeviceType(string name, List<FieldDefineHandle> fields, string? expr, string? description = null);
        public Task RemoveDeviceType(DeviceTypeHandle handle);
        public Task<DeviceType?> GetDeviceType(DeviceTypeHandle handle);
        public Task<DeviceType?> GetDeviceTypeWithFields(DeviceTypeHandle handle);
        public Task ModifyDeviceTypeDescription(DeviceTypeHandle handle, string description);
        public Task<DeviceHandle> AddDevice(DeviceTypeHandle handle, string deviceCode, uint owner);
        public Task<Device?> GetDevice(DeviceHandle handle);
        public Task RemoveDevice(DeviceHandle handle);
        public Task<FieldValueHandle> AddFieldValue(FieldValue value);
        public Task<FieldValue?> GetFieldValue(FieldValueHandle handle);
        public Task RemoveFieldValue(FieldValueHandle handle);
        public Task<FieldValue?> GetLatestFieldValue(DeviceHandle device, FieldDefineHandle field);
        public Task<Device?> GetDeviceWithFields(DeviceHandle handle);
        public Task AddFieldValues(IEnumerable<FieldValue> values);
    }
    public class DeviceDBService : IDeviceService {
        private DeviceCtx ctx;
        public DeviceDBService(DeviceCtx ctx) {
            this.ctx = ctx;
        }
        public async Task<FieldDefineHandle> AddFieldDefinition(string name, FieldType type, string unit, string? description = null) {
            var e = new FieldDefinition(0, name, type, unit);
            e.Description = description;
            ctx.FieldDefinitions.Add(e);
            await ctx.SaveChangesAsync();
            return new FieldDefineHandle(e.Id);
        }
        public async Task<FieldDefinition?> GetFieldDefinition(FieldDefineHandle handle) {
            var e = await ctx.FieldDefinitions.Where(f => f.Id == handle.Id).FirstOrDefaultAsync();
            if (e?.Id != 0) return e;
            return null;
        }
        public async Task RemoveFieldDefinition(FieldDefineHandle handle) {
            await ctx.FieldDefinitions.Where(e => e.Id == handle.Id).ExecuteDeleteAsync();
        }
        public async Task ModifyFieldDefinitionDescription(FieldDefineHandle handle, string description) {
            var e = await this.GetFieldDefinition(handle);
            if (e != null) {
                e.Description = description;
                await ctx.SaveChangesAsync();
            } else {
                ErrorHelper.Throw("不存在字段定义 <" + handle.Id + ">");
            }
        }
        public async Task<DeviceTypeHandle> AddDeviceType(string name, List<FieldDefineHandle> fields, string? expr, string? description = null) {
            List<FieldDefinition> defs = new();
            foreach (var i in fields) {
                var f = await GetFieldDefinition(i);
                if (f == null) ErrorHelper.Throw($"创建<{name}>:<{i.Id}>时字段定义不存在");
                defs.Add(f!);
            }
            var e = new DeviceType(0, name, defs, expr);
            e.Description = description;
            ctx.DeviceTypes.Add(e);
            await ctx.SaveChangesAsync();
            return new(e.Id);
        }
        public async Task RemoveDeviceType(DeviceTypeHandle handle) {
            await ctx.DeviceTypes.Where(e => e.Id == handle.Id).ExecuteDeleteAsync();
        }
        public async Task<DeviceType?> GetDeviceType(DeviceTypeHandle handle) {
            return await ctx.DeviceTypes.Where(e => e.Id == handle.Id).FirstOrDefaultAsync();
        }
        public async Task<DeviceType?> GetDeviceTypeWithFields(DeviceTypeHandle handle) {
            return await ctx.DeviceTypes.Include(d => d.Fields).Where(e => e.Id == handle.Id).FirstOrDefaultAsync();
        }
        public async Task ModifyDeviceTypeDescription(DeviceTypeHandle handle, string description) {
            var e = await this.GetDeviceType(handle);
            if (e != null) {
                e.Description = description;
                await ctx.SaveChangesAsync();
            } else {
                ErrorHelper.Throw("不存在设备类型定义 <" + handle.Id + ">");
            }
        }
        public async Task<DeviceHandle> AddDevice(DeviceTypeHandle handle, string deviceCode, uint owner) {
            var e = await this.GetDeviceType(handle);
            if (e == null) ErrorHelper.Throw($"设备类型<{handle.Id}>不存在");
            var dev = new Device(e!.Id, owner, deviceCode);
            var d = await ctx.Devices.Where(d => d.DeviceCode == deviceCode).FirstOrDefaultAsync();
            if (d != null) return new(d.Id);
            ctx.Devices.Add(dev);
            await ctx.SaveChangesAsync();
            return new(dev.Id);
        }
        public async Task<Device?> GetDevice(DeviceHandle handle) {
            return await ctx.Devices.Where(d => d.Id == handle.Id).FirstOrDefaultAsync();
        }
        public async Task RemoveDevice(DeviceHandle handle) {
            await ctx.Devices.Where(e => e.Id == handle.Id).ExecuteUpdateAsync(setters => setters.SetProperty(d => d.IsDeleted, true));
        }
        public async Task<FieldValueHandle> AddFieldValue(FieldValue value) {
            ctx.FieldValues.Add(value);
            await ctx.SaveChangesAsync();
            return new(value.Id);
        }
        public async Task<FieldValue?> GetFieldValue(FieldValueHandle handle) {
            return await ctx.FieldValues.Where(d => d.Id == handle.Id).FirstOrDefaultAsync();
        }
        public async Task RemoveFieldValue(FieldValueHandle handle) {
            await ctx.FieldValues.Where(e => e.Id == handle.Id).ExecuteDeleteAsync();
        }
        public async Task<FieldValue?> GetLatestFieldValue(DeviceHandle device, FieldDefineHandle field) {
            return await ctx.FieldValues.AsNoTracking().Where(x => x.DeviceId == device.Id && x.FieldDefinitionId == field.Id).OrderByDescending(x => x.Time).ThenByDescending(x => x.Id).FirstOrDefaultAsync();
        }
        public async Task<Device?> GetDeviceWithFields(DeviceHandle handle) {
            return await ctx.Devices.Include(d => d.DeviceType).ThenInclude(dt => dt.Fields).Where(d => d.Id == handle.Id).FirstOrDefaultAsync();
        }
        public async Task AddFieldValues(IEnumerable<FieldValue> values) {
            ctx.FieldValues.AddRange(values);
            await ctx.SaveChangesAsync();
        }
    }
}

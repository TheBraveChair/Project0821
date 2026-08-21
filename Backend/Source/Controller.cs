using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace ASPNET.Source {
    public record class FieldDefBody(string Name, FieldType ValueType, string Unit, string? Description);
    public record class DeviceTypeCreateBody(string Name, string? Description, string? VerifyExpression, List<FieldDefBody> Fields);
    public record class DeviceCreateBody(long DeviceTypeId, string DeviceCode);
    public record class DeviceTypeView(long Id, long DeviceTypeId, uint OwnerId, string DeviceCode);
    public record class DeviceRegisteredEvent(DeviceHandle Handle) : INotification;
    public record class DeviceRemoveEvent(DeviceHandle Handle) : INotification;
    public record class UploadDataBody(long Device, List<ExchangeData.FieldData> Data, DateTimeOffset Time) : INotification;
    public record struct MachineId(uint Id);
    //这个def是用来查询字段定义的
    public record class FieldValueView(long FieldDefId, decimal? DecimalValue, string? StringValue, DateTimeOffset Time);
    public record class RangeRequest(long Device, DateTimeOffset? Start, int Length);
    public record class DeviceTypeBrief(long Id, string Name);

    [ApiController]
    [Route("api/[controller]")]
    //让专门的对外鉴权什么的服务调用这个接口,然后这个放在可信任的内网里,一台管理设备单独管理多个子设备这样
    public class DeviceOps : ControllerBase {
        private readonly IMediator mediator;
        private readonly IDeviceService service;
        private readonly IEnvConfig config;
        private readonly DeviceCtx ctx;
        private readonly IDeviceManager manager;
        private DeviceTypeFactory GetTypeFactory(DeviceTypeCreator typeCreator) {
            return new DeviceTypeFactory(service, typeCreator);
        }
        private DeviceFactory GetDeviceFactory(DeviceCreator deviceCreator) {
            return new DeviceFactory(service, deviceCreator);
        }
        public DeviceOps(IMediator mediator, IDeviceService service, IEnvConfig config, DeviceCtx ctx, IDeviceManager manager) {
            this.mediator = mediator;
            this.service = service;
            this.config = config;
            this.ctx = ctx;
            this.manager = manager;
        }
        [HttpPost("RegisterDeviceType")]
        public async Task<IActionResult> RegisterDeviceType([FromBody] DeviceTypeCreateBody body) {
            var cdt = new DeviceTypeCreator.CreateDeviceType();
            foreach (var i in body.Fields) cdt.HasField(i.Name, i.ValueType, i.Unit, i.Description);
            var handle = await GetTypeFactory(cdt.Create()).Instantiate(body.Name, body.VerifyExpression, body.Description);
            return Ok(handle);
        }
        [HttpPost("GetDeviceType")]
        public async Task<IActionResult> GetDeviceType([FromBody] DeviceTypeHandle handle) {
            var result = await service.GetDeviceTypeWithFields(handle);
            if (result != null) return Ok(result);
            return Ok(new { Value = "找不到设备类型" });
        }
        [HttpPost("RegisterDevice")]
        public async Task<IActionResult> RegisterDevice([FromBody] DeviceCreateBody body) {//如果是调用此api,那么默认注册在这个机器上
            var handle = await GetDeviceFactory(new DeviceCreator(new(body.DeviceTypeId))).Instantiate(body.DeviceCode, config.MachineId);
            await mediator.Publish(new DeviceRegisteredEvent(handle));
            return Ok(handle);
        }
        [HttpPost("GetDevice")]
        public async Task<IActionResult> GetDevice([FromBody] DeviceHandle handle) {
            var device = await service.GetDevice(handle);
            if (device == null) return Ok(new());
            return Ok(new DeviceTypeView(device.Id, device.DeviceTypeId, device.OwnerId, device.DeviceCode));
        }
        [HttpPost("AllDevices")]
        public async Task<IActionResult> AllDevices([FromBody] MachineId id) {
            if (config.MachineId == id.Id) {
                return Ok(manager.AllDevices());
            } else {
                var list = (await ctx.Devices.Where(m => m.OwnerId == id.Id).Select(m => m.Id).ToListAsync()) ?? new();
                return Ok(list);
            }
        }
        [HttpPost("RemoveDeviceType")]
        public async Task<IActionResult> RemoveDeviceType([FromBody] DeviceTypeHandle handle) {
            await service.RemoveDeviceType(handle);
            return Ok();
        }
        [HttpPost("RemoveDevice")]
        public async Task<IActionResult> RemoveDevice([FromBody] DeviceHandle handle) {
            await mediator.Publish(new DeviceRemoveEvent(handle));
            await service.RemoveDevice(handle);
            return Ok();
        }
        [HttpPost("UploadData")]
        public async Task<IActionResult> UploadData([FromBody] UploadDataBody body) {
            _ = mediator.Publish(body);
            return Ok();
        }
        [HttpPost("RangeValue")]
        public async Task<IActionResult> RangeValue([FromBody] RangeRequest request) {
            var start = request.Start;
            var length = request.Length > 0 ? request.Length : 1;
            const int max = 3600;
            if (length > max) return BadRequest(new { message = $"查询跨度不能超过 {max} 秒" });
            if (start == null) start = DateTimeOffset.UtcNow - TimeSpan.FromSeconds(length);
            var begin = start.Value;
            var end = begin + TimeSpan.FromSeconds(length);
            var list = await ctx.FieldValues.AsNoTracking()
                .Where(e => e.DeviceId == request.Device && e.Time >= begin && e.Time < end)
                .OrderBy(e => e.Time)
                .ThenBy(e => e.Id)
                .Select(e => new FieldValueView(e.FieldDefinitionId, e.DecimalValue, e.StringValue, e.Time))
                .ToListAsync();
            return Ok(list);
        }
        [HttpPost("AllDeviceType")]
        public async Task<IActionResult> AllDeviceType() {
            var list = await ctx.DeviceTypes.AsNoTracking().OrderBy(t => t.Id).Select(t => new DeviceTypeBrief(t.Id, t.Name)).ToListAsync();
            return Ok(list);
        }
        [HttpPost("QueryAlertDevices")]
        public async Task<IActionResult> QueryAlertDevices() {
            const long scale = 1L << 32;
            var id = (long)config.MachineId;
            var list = await ctx.WarnTable
                .AsNoTracking()
                .Where(w => w.DeviceId / scale == id)
                .OrderByDescending(w => w.Time)
                .ToListAsync();
            return Ok(list);
        }
        [HttpPost("ConfirmAlert")]
        public async Task<IActionResult> ConfirmAlert([FromBody] DeviceHandle handle) {
            var id = handle.Id;
            await ctx.WarnTable.Where(w => w.DeviceId == id).ExecuteDeleteAsync();
            return Ok();
        }
    }
}
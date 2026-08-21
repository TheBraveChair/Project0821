using Azure.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Frontend.Source {
    public record class RegisterDeviceRequest(uint OwnerId, long DeviceTypeId, string DeviceCode);
    public record class MapRequest(uint Id, uint Owner);
    public record class Empty(string Value);
    public record class MachineMapResponse(List<uint> Machines);
    public record struct MachineId(uint Id);
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class MachineController : ControllerBase {
        private readonly MachineManagerProvider _managerProvider;
        private readonly IDBOp _db;
        public MachineController(MachineManagerProvider managerProvider, IDBOp db) {
            _managerProvider = managerProvider;
            _db = db;
        }
        public MachineManager Manager {
            get {
                var cur = _managerProvider.Current;
                if (cur != null) return cur;
                throw new Exception("后台设备管理服务尚未就绪");
            }
        }
        public uint UserId => uint.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        public string UserName => User.FindFirstValue(ClaimTypes.Name)!;
        public string Role => User.FindFirstValue(ClaimTypes.Role)!;

        [HttpPost("RegisterDeviceType")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RegisterDeviceType([FromBody] DeviceTypeCreateBody body) {
            var handle = await Manager.RegisterDeviceType(body);
            return Ok(handle);
        }
        [HttpPost("GetDeviceType")]
        public async Task<IActionResult> GetDeviceType([FromBody] Handle handle) {
            var result = await Manager.OpGetDeviceType(handle);
            if (result != null) return Ok(result);
            return Ok(new Empty("找不到设备类型"));
        }
        [HttpPost("RemoveDeviceType")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RemoveDeviceType([FromBody] Handle handle) {
            await Manager.OpRemoveDeviceType(handle);
            return Ok();
        }
        [HttpPost("RegisterDevice")]
        [Authorize(Roles = "Operator,Admin")]
        public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceRequest request) {
            var map = await _db.GetMap(UserId);
            if (Role != AdminString && !map.Contains(request.OwnerId)) return Forbid();
            return Ok(await Manager.OpRegisterDevice(request.OwnerId, new DeviceCreateBody(request.DeviceTypeId, request.DeviceCode)));
        }
        public static string AdminString { get; } = RoleType.Admin.ToString();
        [HttpPost("GetDevice")]
        [Authorize]
        public async Task<IActionResult> GetDevice([FromBody] DeviceHandle handle) {
            var map = await _db.GetMap(UserId);
            if (Role != AdminString && (map == null || !map.Contains(handle.OwnerId))) return Forbid();
            var result = await Manager.OpGetDevice(handle);
            if (result != null) return Ok(result);
            return Ok(new Empty("找不到设备"));
        }
        [HttpPost("RemoveDevice")]
        [Authorize(Roles = "Operator,Admin")]
        public async Task<IActionResult> RemoveDevice([FromBody] DeviceHandle handle) {
            var map = await _db.GetMap(UserId);
            if (Role != AdminString && (map == null || !map.Contains(handle.OwnerId))) return Forbid();
            await Manager.OpRemoveDevice(handle);
            return Ok();
        }
        [HttpPost("MyMap")]
        [Authorize]
        public async Task<IActionResult> MyMap() {
            return Ok(await _db.GetMap(uint.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)));
        }
        [HttpPost("GetUserMap")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetMap(UserId id) {
            return Ok(await _db.GetMap(id.Id));
        }
        [HttpPost("AddMap")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddMap([FromBody] MapRequest request) {
            await _db.AddMap(request.Id, request.Owner);
            return Ok();
        }
        [HttpPost("RemoveMap")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RemoveMap([FromBody] MapRequest request) {
            await _db.RemoveMap(request.Id, request.Owner);
            return Ok();
        }
        [HttpPost("GetMachines")]
        [Authorize]
        public async Task<IActionResult> GetMachines() {
            var map = Manager.AllMachinesId;
            if (Role != AdminString) {
                var filter = await _db.GetMap(UserId);
                return Ok(map.Where(filter.Contains).ToList());
            } else {
                return Ok(map);
            }
        }
        private Machine PickMachine(uint ownerId) =>
            Manager.IsExist(ownerId) ? Manager.GetTargetMachine(ownerId) : Manager.RandomMachine;
        [HttpPost("GetDevices")]
        [Authorize]
        public async Task<IActionResult> GetDevices([FromBody] MachineId id) {
            if (Role != AdminString) {
                var filter = await _db.GetMap(UserId);
                if (!filter.Contains(id.Id)) return Forbid();
            }
            Machine m = PickMachine(id.Id);
            var list = await m.OpAllDevices(id.Id);
            return Ok(list ?? new());
        }
        [HttpPost("RangeValue")]
        [Authorize]
        public async Task<IActionResult> RangeValue([FromBody] RangeRequest request) {
            var id = new DeviceHandle(request.Device).OwnerId;
            if (Role != AdminString) {
                var filter = await _db.GetMap(UserId);
                if (!filter.Contains(id)) return Forbid();
            }
            Machine m = PickMachine(id);
            var result = await m.OpRangeValue(request);
            return Ok(result);
        }
        [HttpPost("AllDeviceType")]
        [Authorize(Roles = "Operator,Admin")]
        public async Task<IActionResult> AllDeviceType() {
            return Ok(await Manager.RandomMachine.OpAllDeviceType());
        }
        [HttpPost("QueryAlertDevices")]
        [Authorize(Roles = "Operator,Admin")]
        public async Task<IActionResult> QueryAlertDevices([FromBody] MachineId id) {
            if (Role != AdminString) {
                var filter = await _db.GetMap(UserId);
                if (!filter.Contains(id.Id)) return Forbid();
            }
            if (!Manager.IsExist(id.Id)) return Forbid();
            var list = await Manager.GetTargetMachine(id.Id).OpQueryAlertDevices();
            return Ok(list);
        }
        [HttpPost("ConfirmAlert")]
        [Authorize(Roles = "Operator,Admin")]
        public async Task<IActionResult> ConfirmAlert([FromBody] DeviceHandle handle) {
            if (Role != AdminString) {
                var filter = await _db.GetMap(UserId);
                if (!filter.Contains(handle.OwnerId)) return Forbid();
            }
            if (!Manager.IsExist(handle.OwnerId)) return Forbid();
            await Manager.GetTargetMachine(handle.OwnerId).OpConfirmAlert(new(handle.Id));
            return Ok();
        }
    }
}

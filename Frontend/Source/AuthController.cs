using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Frontend.Source {
    public record class LoginRequest(string Username, string Password);
    public record class RegisterRequest(string Username, string Password, RoleType Role);
    public record class UserToken(uint Id, RoleType Role, string Token);
    public record class ModifyRequest(uint Id, RoleType Role);
    public record struct UserId(uint Id);
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase {
        private IDBOp _db;
        private JwtGenerater _jwtGenerater;
        public AuthController(IDBOp db, JwtGenerater jwtGenerater) {
            _db = db;
            _jwtGenerater = jwtGenerater;
        }
        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request) {
            var user = await _db.GetAndVerify(request.Username, request.Password);
            if (user == null) return Unauthorized(new { Message = "用户名或密码错误" });
            return Ok(new UserToken(user.Id, user.Role, _jwtGenerater.GenerateToken(user)));
        }
        [HttpPost("Register")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request) {
            var user = await _db.AddUser(request.Username, request.Password, request.Role);
            return Ok(new { Id = user });
        }
        [HttpPost("RemoveUser")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Remove([FromBody] UserId handle) {
            await _db.RemoveUser(handle.Id);
            return Ok();
        }
        [HttpPost("ModifyRole")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Modify([FromBody] ModifyRequest request) {
            await _db.ModifyRole(request.Id, request.Role);
            return Ok();
        }
    }
}

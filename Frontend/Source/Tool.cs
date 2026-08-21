using MessagePack;
using MessagePack.Resolvers;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using static Frontend.Program;

namespace Frontend.Source {
    public static class SerializerTool {
        private static readonly MessagePackSerializerOptions _msgPackOptions = MessagePackSerializerOptions.Standard.WithResolver(ContractlessStandardResolver.Instance);
        public static T Deserialize<T>(byte[] data) {
            return MessagePackSerializer.Deserialize<T>(data, _msgPackOptions);
        }
        public static byte[] Serialize<T>(T obj) {
            return MessagePackSerializer.Serialize(obj, _msgPackOptions);
        }
    }
    public static class JwtHelper {
        public static string GenerateJwtToken(User user, JwtKey jwtKey, Config config) {
            var tokenHandler = new JwtSecurityTokenHandler();
            var claims = new List<Claim> {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };
            var tokenDescriptor = new SecurityTokenDescriptor {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(config.JwtExpireMinutes),
                Issuer = config.JwtIssuer,
                Audience = config.JwtAudience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(jwtKey.JwtKeys), SecurityAlgorithms.HmacSha256Signature)
            };
            return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
        }
    }
    public class JwtGenerater {
        private Config _config;
        private JwtKey _key;
        public JwtGenerater(Config config, JwtKey key) {
            _config = config;
            _key = key;
        }
        public string GenerateToken(User user) => JwtHelper.GenerateJwtToken(user, _key, _config);
    }
    public static class PasswordGenerator {
        private const string Lowercase = "abcdefghijklmnopqrstuvwxyz";
        private const string Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const string Digits = "0123456789";
        private const string Special = "!@#$%^&*";
        public static string Generate(int length = 16) {
            if (length < 8) length = 8;
            string charPool = Lowercase + Uppercase + Digits + Special;
            var result = new StringBuilder();
            var bytes = new byte[length];
            using (var rng = RandomNumberGenerator.Create()) {
                rng.GetBytes(bytes);
            }
            for (int i = 0; i < length; i++) {
                int index = bytes[i] % charPool.Length;
                result.Append(charPool[index]);
            }
            return result.ToString();
        }
    }
}

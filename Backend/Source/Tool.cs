using MessagePack;
using MessagePack.Resolvers;

namespace ASPNET.Source {
    public static class ErrorHelper {
        public static void Throw(string message) {
            throw new Exception(message);
        }
    }
    public static class SerializerTool {
        private static readonly MessagePackSerializerOptions _msgPackOptions = MessagePackSerializerOptions.Standard.WithResolver(ContractlessStandardResolver.Instance);
        public static T Deserialize<T>(byte[] data) {
            return MessagePackSerializer.Deserialize<T>(data, _msgPackOptions);
        }
        public static byte[] Serialize<T>(T obj) {
            return MessagePackSerializer.Serialize(obj, _msgPackOptions);
        }
    }
}

using System.Text;

namespace Frontend.Source {
    public enum FieldType {
        String = 1,
        Decimal = 2
    }
    public record struct Handle(long Id);
    public record class FieldDefBody(string Name, FieldType ValueType, string Unit, string? Description);
    public record class DeviceTypeCreateBody(string Name, string? Description, string? VerifyExpression, List<FieldDefBody> Fields);
    public record class DeviceCreateBody(long DeviceTypeId, string DeviceCode);
    public record class DeviceTypeView(long Id, long DeviceTypeId, uint OwnerId, string DeviceCode);
    public record class FieldDefinitionView(long Id, string Name, FieldType Type, string? Description, string? Unit);
    public record class DeviceTypeWithFieldsView(long Id, string Name, string? VerifyExpression, string? Description, List<FieldDefinitionView> Fields);
    public record class FieldValueView(long FieldDefId, decimal? DecimalValue, string? StringValue, DateTimeOffset Time);
    public record class RangeRequest(long Device, DateTimeOffset? Start, int Length);
    public record class DeviceTypeBrief(long Id, string Name);
    public record class Warn(long DeviceId, DateTimeOffset Time);
    public class Machine {
        public Machine(uint owner, string address) {
            this.Owner = owner;
            this.Address = address;
        }
        private HttpClient client = new();
        ~Machine() {
            client.Dispose();
        }
        public uint Owner { get; }
        public string Address { get; }
        public string ApiUrl => $"{(Address.StartsWith("http://") || Address.StartsWith("https://") ? "" : "http://")}{Address}/api/DeviceOps/";
        public string RegisterDeviceType => ApiUrl + "RegisterDeviceType";
        public string GetDeviceType => ApiUrl + "GetDeviceType";
        public string RegisterDevice => ApiUrl + "RegisterDevice";
        public string GetDevice => ApiUrl + "GetDevice";
        public string RemoveDeviceType => ApiUrl + "RemoveDeviceType";
        public string RemoveDevice => ApiUrl + "RemoveDevice";
        public string AllDevices => ApiUrl + "AllDevices";
        public string RangeValue => ApiUrl + "RangeValue";
        public string AllDeviceType => ApiUrl + "AllDeviceType";
        public string QueryAlertDevices => ApiUrl + "QueryAlertDevices";
        public string ConfirmAlert => ApiUrl + "ConfirmAlert";
        public async Task<TResult?> OpPost<TResult, TBody>(string uri, TBody body) {
            var resp = await client.PostAsJsonAsync(uri, body);
            if (!resp.IsSuccessStatusCode) {
                var errorContent = await resp.Content.ReadAsStringAsync();
                throw new Exception($"后端服务拒绝了请求: 状态码 {resp.StatusCode}。详细信息: {errorContent}");
            }
            var result = await resp.Content.ReadFromJsonAsync<TResult>();
            return result;
        }

        public async Task OpPost<TBody>(string uri, TBody body) {
            var resp = await client.PostAsJsonAsync(uri, body);
            if (!resp.IsSuccessStatusCode) {
                var errorContent = await resp.Content.ReadAsStringAsync();
                throw new Exception($"后端服务拒绝了请求: 状态码 {resp.StatusCode}。详细信息: {errorContent}");
            }
        }
        public async Task<T?> OpPost<T>(string uri) {
            using var ctx = new StringContent("{}", Encoding.UTF8, "application/json");
            var resp = await client.PostAsync(uri, ctx);
            if (!resp.IsSuccessStatusCode) {
                var errorContent = await resp.Content.ReadAsStringAsync();
                throw new Exception($"后端服务拒绝了请求: 状态码 {resp.StatusCode}。详细信息: {errorContent}");
            }
            var result = await resp.Content.ReadFromJsonAsync<T>();
            return result;
        }

        public async Task<Handle> OpRegisterDeviceType(DeviceTypeCreateBody body)
           => await OpPost<Handle, DeviceTypeCreateBody>(RegisterDeviceType, body);
        public async Task<DeviceTypeWithFieldsView?> OpGetDeviceType(Handle handle)
          => await OpPost<DeviceTypeWithFieldsView, Handle>(GetDeviceType, handle);
        public async Task<Handle> OpRegisterDevice(DeviceCreateBody body)
            => await OpPost<Handle, DeviceCreateBody>(RegisterDevice, body);
        public async Task<DeviceTypeView?> OpGetDevice(Handle handle)
            => await OpPost<DeviceTypeView, Handle>(GetDevice, handle);
        public async Task OpRemoveDeviceType(Handle handle)
            => await OpPost(RemoveDeviceType, handle);
        public async Task OpRemoveDevice(Handle handle)
            => await OpPost(RemoveDevice, handle);
        public async Task<List<long>?> OpAllDevices(uint machineId)
            => await OpPost<List<long>, MachineId>(AllDevices, new MachineId(machineId));
        public async Task<List<FieldValueView>?> OpRangeValue(RangeRequest request)
            => await OpPost<List<FieldValueView>, RangeRequest>(RangeValue, request);
        public async Task<List<DeviceTypeBrief>?> OpAllDeviceType()
            => await OpPost<List<DeviceTypeBrief>>(AllDeviceType);
        public async Task OpConfirmAlert(Handle handle)
            => await OpPost(ConfirmAlert, handle);
        public async Task<List<Warn>?> OpQueryAlertDevices()
            => await OpPost<List<Warn>>(QueryAlertDevices);
    }
}

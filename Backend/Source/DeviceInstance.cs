using ASPNET.Source.Expression;

namespace ASPNET.Source {
    public record struct InitField(FieldType Type, decimal? DecimalValue, string? StringValue, string Name,long FieldId);
    public class DeviceInstance {
        public DeviceHandle Device { get; }
        private Expression.Environment env = new();
        public DateTimeOffset LastUpdateTime { get; set; }
        public bool Invalid => Device.Id == 0;
        public DeviceInstance(DeviceHandle device) {
            this.Device = device;
        }
        public List<FieldValueStruct> GetData() => env.GetData();
        public void InitExpr(string expr) => env.InitExpr(expr);
        public bool IsTrue() => env.Eval();
        public void UpdateConfig(List<InitField>? values) {
            env.Clear();
            if (Invalid) return;
            if (values != null) {
                foreach (var field in values) {
                    switch (field.Type) {
                        case FieldType.String:
                            env.SetValue(field.Name, field.StringValue ?? "",field.FieldId);
                            break;
                        case FieldType.Decimal:
                            env.SetValue(field.Name, field.DecimalValue ?? 0, field.FieldId);
                            break;
                    }
                }
            }
        }
        public void UpdateValue(string name, decimal value) => env.SetValue(name, value, 0);
    }
}

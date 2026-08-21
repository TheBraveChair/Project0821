namespace ASPNET.Source {
    public class DeviceCreator {
        private DeviceTypeHandle _handle;
        public DeviceCreator(DeviceTypeHandle handle) {
            this._handle = handle;
        }
        public async Task<DeviceHandle> Instantiate(IDeviceService service, string deviceCode, uint ownerId) {
            return await service.AddDevice(_handle, deviceCode, ownerId);
        }
    }
    public class DeviceTypeCreator {
        public struct CreateDeviceType {
            internal List<FieldDefinition> _fields = new();
            public CreateDeviceType() { }
            public CreateDeviceType HasField(long fieldId) {
                if (_fields.Where(e => e.Id == fieldId).FirstOrDefault() != null) return this;
                this._fields.Add(new FieldDefinition(fieldId, "", FieldType.String, ""));
                return this;
            }
            public CreateDeviceType HasField(string name, FieldType type, string unit, string? description = null) {
                var def = new FieldDefinition(0, name, type, unit);
                def.Description = description;
                this._fields.Add(def);
                return this;
            }
            public DeviceTypeCreator Create() {
                return new DeviceTypeCreator(this);
            }
        }
        private CreateDeviceType _type;
        internal DeviceTypeCreator(CreateDeviceType type) {
            this._type = type;
        }
        public async Task<DeviceTypeHandle> Instantiate(IDeviceService service, string name, string? expr, string? description) {
            var refs = new List<FieldDefineHandle>();
            foreach (var i in _type._fields) {
                if (i.Id != 0) refs.Add(new(i.Id));
                else {
                    var handle = await service.AddFieldDefinition(i.Name, i.Type, i.Unit ?? "", i.Description);
                    refs.Add(handle);
                    i.Id = handle.Id;
                }
            }
            var deviceType = await service.AddDeviceType(name, refs, expr, description);
            return deviceType;
        }
    }
    public class DeviceFactory {
        private readonly IDeviceService _service;
        private readonly DeviceCreator _creator;
        public DeviceFactory(IDeviceService service, DeviceCreator creator) {
            _service = service;
            _creator = creator;
        }
        public async Task<DeviceHandle> Instantiate(string deviceCode, uint ownerId) {
            return await _creator.Instantiate(_service, deviceCode, ownerId);
        }
    }
    public class DeviceTypeFactory {
        private readonly IDeviceService _service;
        private readonly DeviceTypeCreator _creator;
        public DeviceTypeFactory(IDeviceService service, DeviceTypeCreator creator) {
            _service = service;
            _creator = creator;
        }
        public async Task<DeviceTypeHandle> Instantiate(string name, string? expr, string? description) {
            return await _creator.Instantiate(_service, name, expr, description);
        }
    }
}

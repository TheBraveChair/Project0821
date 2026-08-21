using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace ASPNET.Source {
    public class DeviceIdValueGenerator : ValueGenerator<long> {
        public override bool GeneratesTemporaryValues => false;
        public override long Next(EntityEntry entry) {
            if (entry.Context is DeviceCtx ctx) return ctx.IdGenerator.NextId();
            throw new InvalidOperationException("DbContext 必须是 DeviceCtx");
        }
    }
}
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;

namespace monitoring_service.Model
{
    public class Device
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Consumption { get; set; }
    }
}

namespace monitoring_service.Model
{
    public class Consumption
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }
        public DateTime Timestamp { get; set; }
        public double ConsumptionValue { get; set; }
    }
}

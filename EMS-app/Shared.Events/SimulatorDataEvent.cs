namespace Shared.Events
{
    public class SimulatorDataEvent
    {
        public Guid DeviceId { get; set; }
        public double ConsumptionValue { get; set; }
        public DateTime Timestamp { get; set; }
        public string CorrelationId { get; set; } = string.Empty;
    }
}
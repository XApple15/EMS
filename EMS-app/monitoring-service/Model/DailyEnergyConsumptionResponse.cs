namespace monitoring_service.Model
{
    public class DailyEnergyConsumptionResponse
    {
        public string ClientId { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public List<HourlyEnergyConsumption> Data { get; set; } = new List<HourlyEnergyConsumption>();
    }
}
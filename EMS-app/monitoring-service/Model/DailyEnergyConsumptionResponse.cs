namespace monitoring_service.Model
{
    /// <summary>
    /// Response DTO for daily energy consumption data
    /// </summary>
    public class DailyEnergyConsumptionResponse
    {
        /// <summary>
        /// The client/user ID
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// The date for the energy consumption data (YYYY-MM-DD format)
        /// </summary>
        public string Date { get; set; } = string.Empty;

        /// <summary>
        /// Hourly energy consumption data (24 data points)
        /// </summary>
        public List<HourlyEnergyConsumption> Data { get; set; } = new List<HourlyEnergyConsumption>();
    }
}

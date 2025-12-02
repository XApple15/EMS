namespace monitoring_service.Model
{
    /// <summary>
    /// Represents energy consumption data for a single hour
    /// </summary>
    public class HourlyEnergyConsumption
    {
        /// <summary>
        /// Hour of the day (0-23)
        /// </summary>
        public int Hour { get; set; }

        /// <summary>
        /// Energy consumption in kilowatt-hours (kWh)
        /// </summary>
        public double EnergyKwh { get; set; }
    }
}

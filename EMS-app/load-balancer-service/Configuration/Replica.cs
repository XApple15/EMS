namespace load_balancer_service.Configuration
{
    /// <summary>
    /// Represents a monitoring service replica configuration
    /// </summary>
    public class Replica
    {
        /// <summary>
        /// Unique identifier for the replica
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Weight for weighted distribution strategies (higher = more load)
        /// </summary>
        public int Weight { get; set; } = 1;

        /// <summary>
        /// Current load percentage (0-100) - used by load-based selectors
        /// </summary>
        public double LoadPercentage { get; set; } = 0;

        /// <summary>
        /// Whether the replica is currently healthy and available
        /// </summary>
        public bool IsHealthy { get; set; } = true;
    }
}

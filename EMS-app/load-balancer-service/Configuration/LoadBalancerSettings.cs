namespace load_balancer_service.Configuration
{
    /// <summary>
    /// Configuration settings for the Load Balancer Service
    /// </summary>
    public class LoadBalancerSettings
    {
        /// <summary>
        /// Load balancing strategy to use (ConsistentHashing, LoadBased, WeightedRoundRobin)
        /// </summary>
        public string Strategy { get; set; } = "ConsistentHashing";

        /// <summary>
        /// Name of the central queue to consume device data from
        /// </summary>
        public string CentralQueueName { get; set; } = "device-data-queue";

        /// <summary>
        /// Pattern for ingest queue names. Use {0} as placeholder for replica ID
        /// </summary>
        public string IngestQueuePattern { get; set; } = "ingest-queue-{0}";

        /// <summary>
        /// List of available monitoring service replicas
        /// </summary>
        public List<Replica> Replicas { get; set; } = new List<Replica>();

        /// <summary>
        /// RabbitMQ exchange name for routing messages
        /// </summary>
        public string ExchangeName { get; set; } = "device.data";

        /// <summary>
        /// RabbitMQ exchange type
        /// </summary>
        public string ExchangeType { get; set; } = "direct";
    }
}

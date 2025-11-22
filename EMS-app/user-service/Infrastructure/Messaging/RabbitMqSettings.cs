namespace user_service.Infrastructure.Messaging
{
    /// <summary>
    /// Configuration settings for RabbitMQ connection and messaging
    /// </summary>
    public class RabbitMqSettings
    {
        /// <summary>
        /// RabbitMQ host name or IP address
        /// </summary>
        public string HostName { get; set; } = "rabbitmq";

        /// <summary>
        /// RabbitMQ port number
        /// </summary>
        public int Port { get; set; } = 5672;

        /// <summary>
        /// RabbitMQ username for authentication
        /// </summary>
        public string UserName { get; set; } = "guest";

        /// <summary>
        /// RabbitMQ password for authentication
        /// </summary>
        public string Password { get; set; } = "guest";

        /// <summary>
        /// Exchange name for topic-based messaging
        /// </summary>
        public string ExchangeName { get; set; } = "user.events";

        /// <summary>
        /// Exchange type (topic, direct, fanout, headers)
        /// </summary>
        public string ExchangeType { get; set; } = "topic";

        /// <summary>
        /// Queue name for this consumer
        /// </summary>
        public string QueueName { get; set; } = "user-service-queue";

        /// <summary>
        /// Maximum number of retry attempts for operations
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Initial delay in milliseconds between retry attempts
        /// </summary>
        public int RetryDelayMilliseconds { get; set; } = 1000;
    }
}

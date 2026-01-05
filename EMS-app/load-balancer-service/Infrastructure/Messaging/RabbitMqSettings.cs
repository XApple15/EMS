namespace load_balancer_service.Infrastructure.Messaging
{
    /// <summary>
    /// RabbitMQ connection settings
    /// </summary>
    public class RabbitMqSettings
    {
        public string HostName { get; set; } = "rabbitmq";
        public int Port { get; set; } = 5672;
        public string UserName { get; set; } = "admin";
        public string Password { get; set; } = "admin123";
        public int MaxRetryAttempts { get; set; } = 3;
        public int RetryDelayMilliseconds { get; set; } = 1000;
    }
}

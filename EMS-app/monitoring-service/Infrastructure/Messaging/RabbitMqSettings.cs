namespace monitoring_service.Infrastructure.Messaging
{
    public class RabbitMqSettings
    {
        public string HostName { get; set; } = "rabbitmq";
        public int Port { get; set; } = 5672;
        public string UserName { get; set; } = "admin";
        public string Password { get; set; } = "admin123";
        public string ExchangeName { get; set; } = "user.events";
        public string NotificationsQueue { get; set; } = "user_notifications";
        
        public string ExchangeType { get; set; } = "topic";
        public string QueueName { get; set; } = "monitoring-service-queue";
        
        // Load balancer settings
        public string ReplicaId { get; set; } = "1";
        public string IngestQueuePattern { get; set; } = "ingest-queue-{0}";
        public string DeviceDataExchange { get; set; } = "device.data";
        
        public int MaxRetryAttempts { get; set; } = 3;
        public int RetryDelayMilliseconds { get; set; } = 1000;
    }
}
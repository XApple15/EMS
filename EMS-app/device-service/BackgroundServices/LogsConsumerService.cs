using device_service.Infrastructure.Messaging;

namespace device_service.BackgroundServices
{
    /// <summary>
    /// Background service that consumes log messages from the "logs" fanout exchange.
    /// Follows the official RabbitMQ .NET tutorial pattern for publish/subscribe.
    /// </summary>
    public class LogsConsumerService : BackgroundService
    {
        private readonly ILogsConsumer _logsConsumer;
        private readonly ILogger<LogsConsumerService> _logger;

        public LogsConsumerService(
            ILogsConsumer logsConsumer,
            ILogger<LogsConsumerService> logger)
        {
            _logsConsumer = logsConsumer;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("LogsConsumerService starting...");

            try
            {
                await _logsConsumer.StartConsumingAsync(
                    handler: HandleLogMessageAsync,
                    cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in LogsConsumerService");
            }
        }

        /// <summary>
        /// Handles incoming log messages from the fanout exchange
        /// </summary>
        /// <param name="message">The log message received</param>
        private Task HandleLogMessageAsync(string message)
        {
            _logger.LogInformation("[x] Received log: {Message}", message);
            return Task.CompletedTask;
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("LogsConsumerService stopping...");
            return base.StopAsync(cancellationToken);
        }
    }
}

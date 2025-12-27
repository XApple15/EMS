using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace device_service.Infrastructure.Messaging
{
    public class RabbitMqConnectionFactory : IRabbitMqConnectionFactory
    {
        private readonly RabbitMqSettings _settings;
        private readonly ILogger<RabbitMqConnectionFactory> _logger;
        private IConnection? _connection;
        private readonly object _lock = new object();

        public RabbitMqConnectionFactory(
            IOptions<RabbitMqSettings> settings,
            ILogger<RabbitMqConnectionFactory> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public IConnection CreateConnection()
        {
            if (_connection != null && _connection.IsOpen)
            {
                return _connection;
            }

            lock (_lock)
            {
                if (_connection != null && _connection.IsOpen)
                {
                    return _connection;
                }

                try
                {
                    var factory = new ConnectionFactory
                    {
                        HostName = _settings.HostName,
                        Port = _settings.Port,
                        UserName = _settings.UserName,
                        Password = _settings.Password,
                        AutomaticRecoveryEnabled = true,
                        NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                        RequestedHeartbeat = TimeSpan.FromSeconds(60)
                    };

                    _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
                    _logger.LogInformation("RabbitMQ connection established to {HostName}:{Port}",
                        _settings.HostName, _settings.Port);

                    return _connection;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create RabbitMQ connection to {HostName}:{Port}",
                        _settings.HostName, _settings.Port);
                    throw;
                }
            }
        }
    }
}
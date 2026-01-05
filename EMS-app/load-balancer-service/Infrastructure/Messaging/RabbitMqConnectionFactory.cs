using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace load_balancer_service.Infrastructure.Messaging
{
    /// <summary>
    /// Thread-safe factory for creating RabbitMQ connections
    /// </summary>
    public class RabbitMqConnectionFactory : IRabbitMqConnectionFactory, IDisposable
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
                    _logger.LogInformation(
                        "RabbitMQ connection established: {HostName}:{Port}",
                        _settings.HostName, _settings.Port);
                    return _connection;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to create RabbitMQ connection: {HostName}:{Port}",
                        _settings.HostName, _settings.Port);
                    throw;
                }
            }
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}

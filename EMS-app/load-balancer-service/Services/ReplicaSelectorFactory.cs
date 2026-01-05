using load_balancer_service.Configuration;
using Microsoft.Extensions.Options;

namespace load_balancer_service.Services
{
    /// <summary>
    /// Factory for creating replica selectors based on configuration
    /// </summary>
    public class ReplicaSelectorFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly LoadBalancerSettings _settings;
        private readonly ILogger<ReplicaSelectorFactory> _logger;

        public ReplicaSelectorFactory(
            IServiceProvider serviceProvider,
            IOptions<LoadBalancerSettings> settings,
            ILogger<ReplicaSelectorFactory> logger)
        {
            _serviceProvider = serviceProvider;
            _settings = settings.Value;
            _logger = logger;
        }

        public IReplicaSelector CreateSelector()
        {
            var strategy = _settings.Strategy;
            
            _logger.LogInformation("Creating replica selector with strategy: {Strategy}", strategy);

            return strategy.ToLowerInvariant() switch
            {
                "consistenthashing" => _serviceProvider.GetRequiredService<ConsistentHashingSelector>(),
                "loadbased" => _serviceProvider.GetRequiredService<LoadBasedSelector>(),
                "weightedroundrobin" => _serviceProvider.GetRequiredService<WeightedRoundRobinSelector>(),
                _ => throw new InvalidOperationException($"Unknown replica selection strategy: {strategy}")
            };
        }
    }
}

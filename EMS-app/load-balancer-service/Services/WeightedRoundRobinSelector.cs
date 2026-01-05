using load_balancer_service.Configuration;

namespace load_balancer_service.Services
{
    /// <summary>
    /// Implements weighted round-robin replica selection
    /// Distributes load based on replica weights
    /// </summary>
    public class WeightedRoundRobinSelector : IReplicaSelector
    {
        private readonly ILogger<WeightedRoundRobinSelector> _logger;
        private int _currentIndex = 0;
        private int _currentWeight = 0;
        private readonly object _lock = new object();

        public WeightedRoundRobinSelector(ILogger<WeightedRoundRobinSelector> logger)
        {
            _logger = logger;
        }

        public Replica? SelectReplica(string messageKey, IEnumerable<Replica> availableReplicas)
        {
            var replicas = availableReplicas.Where(r => r.IsHealthy).ToList();
            
            if (replicas.Count == 0)
            {
                _logger.LogWarning("No healthy replicas available for selection");
                return null;
            }

            if (replicas.Count == 1)
            {
                return replicas[0];
            }

            lock (_lock)
            {
                var maxWeight = replicas.Max(r => r.Weight);
                var totalWeight = replicas.Sum(r => r.Weight);

                while (true)
                {
                    _currentIndex = (_currentIndex + 1) % replicas.Count;
                    
                    if (_currentIndex == 0)
                    {
                        _currentWeight = _currentWeight - 1;
                        if (_currentWeight <= 0)
                        {
                            _currentWeight = maxWeight;
                        }
                    }

                    var replica = replicas[_currentIndex];
                    if (replica.Weight >= _currentWeight)
                    {
                        _logger.LogDebug(
                            "Weighted round-robin selected replica {ReplicaId} (weight: {Weight}) for message key {MessageKey}",
                            replica.Id, replica.Weight, messageKey);
                        return replica;
                    }
                }
            }
        }
    }
}

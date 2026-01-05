using load_balancer_service.Configuration;

namespace load_balancer_service.Services
{
    /// <summary>
    /// Implements load-based replica selection
    /// Routes to the replica with the lowest current load
    /// </summary>
    public class LoadBasedSelector : IReplicaSelector
    {
        private readonly ILogger<LoadBasedSelector> _logger;

        public LoadBasedSelector(ILogger<LoadBasedSelector> logger)
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

            // Select replica with lowest load percentage
            var selected = replicas.OrderBy(r => r.LoadPercentage).First();

            _logger.LogDebug(
                "Load-based selector chose replica {ReplicaId} with load {LoadPercentage}% for message key {MessageKey}",
                selected.Id, selected.LoadPercentage, messageKey);

            return selected;
        }
    }
}

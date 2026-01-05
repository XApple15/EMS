using load_balancer_service.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace load_balancer_service.Services
{
    /// <summary>
    /// Implements consistent hashing for replica selection
    /// Ensures even distribution and minimal reassignment when replicas change
    /// </summary>
    public class ConsistentHashingSelector : IReplicaSelector
    {
        private readonly ILogger<ConsistentHashingSelector> _logger;

        public ConsistentHashingSelector(ILogger<ConsistentHashingSelector> logger)
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

            // Compute hash of the message key
            var hash = ComputeHash(messageKey);

            // Sort replicas by their hash and select the first one >= message hash
            var replicaHashes = replicas
                .Select(r => new { Replica = r, Hash = ComputeHash(r.Id) })
                .OrderBy(x => x.Hash)
                .ToList();

            // Find the first replica with hash >= message hash
            var selected = replicaHashes.FirstOrDefault(x => x.Hash >= hash);
            
            // If no replica hash is >= message hash, wrap around to the first replica
            if (selected == null)
            {
                selected = replicaHashes[0];
            }

            _logger.LogDebug(
                "Consistent hashing selected replica {ReplicaId} for message key {MessageKey}",
                selected.Replica.Id, messageKey);

            return selected.Replica;
        }

        private uint ComputeHash(string input)
        {
            using var md5 = MD5.Create();
            var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
            
            // Convert first 4 bytes to uint
            return BitConverter.ToUInt32(hashBytes, 0);
        }
    }
}

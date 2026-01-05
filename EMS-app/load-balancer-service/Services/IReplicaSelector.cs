using load_balancer_service.Configuration;

namespace load_balancer_service.Services
{
    /// <summary>
    /// Interface for replica selection strategies
    /// </summary>
    public interface IReplicaSelector
    {
        /// <summary>
        /// Selects a replica based on the message key and available replicas
        /// </summary>
        /// <param name="messageKey">Key to use for selection (e.g., device ID)</param>
        /// <param name="availableReplicas">List of available healthy replicas</param>
        /// <returns>Selected replica, or null if no replicas available</returns>
        Replica? SelectReplica(string messageKey, IEnumerable<Replica> availableReplicas);
    }
}

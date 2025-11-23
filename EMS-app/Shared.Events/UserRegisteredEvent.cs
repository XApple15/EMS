namespace Shared.Events
{
    /// <summary>
    /// Event published when a user is registered in the system
    /// </summary>
    public class UserRegisteredEvent
    {
        /// <summary>
        /// Unique identifier for the user
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        

        /// <summary>
        /// User's username
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// User's  address
        /// </summary>
        public string Address { get; set; } = string.Empty;

        

        /// <summary>
        /// Timestamp when the user was registered
        /// </summary>
        public DateTime RegisteredAt { get; set; }

        /// <summary>
        /// Correlation ID for distributed tracing
        /// </summary>
        public string CorrelationId { get; set; } = string.Empty;
    }
}

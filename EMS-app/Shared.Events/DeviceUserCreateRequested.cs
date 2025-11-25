namespace Shared.Events
{
    /// <summary>
    /// Event published when user-service creates a user profile and device-service should create a corresponding user record.
    /// This event is consumed only by device-service using a dedicated routing key to avoid event loops.
    /// </summary>
    /// <remarks>
    /// Sample JSON payload:
    /// <code>
    /// {
    ///     "UserId": "550e8400-e29b-41d4-a716-446655440000",
    ///     "Email": "user@example.com",
    ///     "Username": "johndoe",
    ///     "Address": "123 Main Street",
    ///     "RegisteredAt": "2024-11-25T12:00:00Z",
    ///     "CorrelationId": "660e8400-e29b-41d4-a716-446655440001"
    /// }
    /// </code>
    /// 
    /// Routing key: user.device.create
    /// Exchange: user.events
    /// </remarks>
    public class DeviceUserCreateRequested
    {
        /// <summary>
        /// Unique identifier for the user (from auth-service, maps to AuthId in user tables)
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// User's email address
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// User's username
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// User's address
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

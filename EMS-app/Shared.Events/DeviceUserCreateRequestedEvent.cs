namespace Shared.Events
{
    /// <summary>
    /// Event published when a user is created in user-service, intended for device-service to create a corresponding user record.
    /// This event is published after successful user creation in user-service.
    /// 
    /// <example>
    /// Sample JSON payload:
    /// <code>
    /// {
    ///     "UserId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
    ///     "Username": "johndoe",
    ///     "Address": "123 Main St",
    ///     "CreatedAt": "2024-11-25T10:30:00Z",
    ///     "CorrelationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
    /// }
    /// </code>
    /// </example>
    /// 
    /// Routing Key: user.created.device
    /// Exchange: user.events
    /// </summary>
    public class DeviceUserCreateRequestedEvent
    {
        /// <summary>
        /// Unique identifier for the user (matches AuthId in user-service and device-service)
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// User's username
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// User's address
        /// </summary>
        public string Address { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when the user was created in user-service
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Correlation ID for distributed tracing
        /// </summary>
        public string CorrelationId { get; set; } = string.Empty;
    }
}
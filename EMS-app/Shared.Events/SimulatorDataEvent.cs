namespace Shared.Events
{
    /// <summary>
    /// Event published by the simulator service containing device telemetry data.
    /// Used for communication between the simulator and monitoring services.
    /// 
    /// <example>
    /// Sample JSON payload:
    /// <code>
    /// {
    ///     "DeviceId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
    ///     "DeviceName": "Smart Meter 001",
    ///     "ConsumptionValue": 125.5,
    ///     "Unit": "kWh",
    ///     "Timestamp": "2024-11-26T10:30:00Z",
    ///     "CorrelationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
    /// }
    /// </code>
    /// </example>
    /// 
    /// Routing Key: simulator.data
    /// Exchange: simulator.events
    /// </summary>
    public class SimulatorDataEvent
    {
        /// <summary>
        /// Unique identifier for the device
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Name of the device
        /// </summary>
        public string DeviceName { get; set; } = string.Empty;

        /// <summary>
        /// The consumption value recorded by the device
        /// </summary>
        public double ConsumptionValue { get; set; }

        /// <summary>
        /// Unit of measurement (e.g., kWh, W, A)
        /// </summary>
        public string Unit { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when the data was recorded
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Correlation ID for distributed tracing
        /// </summary>
        public string CorrelationId { get; set; } = string.Empty;
    }
}

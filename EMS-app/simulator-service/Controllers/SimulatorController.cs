using Microsoft.AspNetCore.Mvc;
using Shared.Events;
using simulator_service.Infrastructure.Messaging;

namespace simulator_service.Controllers
{
    /// <summary>
    /// Controller for managing simulator data and publishing events to the monitoring service
    /// </summary>
    [ApiController]
    [Route("[controller]")]
    public class SimulatorController : ControllerBase
    {
        private readonly IEventPublisher _eventPublisher;
        private readonly ILogger<SimulatorController> _logger;

        /// <summary>
        /// Routing key used for publishing simulator data events.
        /// Monitoring-service consumes events with this routing key.
        /// </summary>
        private const string SimulatorDataRoutingKey = "simulator.data";

        public SimulatorController(
            IEventPublisher eventPublisher,
            ILogger<SimulatorController> logger)
        {
            _eventPublisher = eventPublisher;
            _logger = logger;
        }

        /// <summary>
        /// Publishes simulated device data to the monitoring service via RabbitMQ
        /// </summary>
        /// <param name="request">The simulator data to publish</param>
        /// <returns>Result of the publish operation</returns>
        [HttpPost("publish")]
        public async Task<IActionResult> PublishSimulatorData([FromBody] SimulatorDataRequest request)
        {
            var correlationId = Guid.NewGuid().ToString();

            try
            {
                var simulatorEvent = new SimulatorDataEvent
                {
                    DeviceId = request.DeviceId,
                    DeviceName = request.DeviceName,
                    ConsumptionValue = request.ConsumptionValue,
                    Unit = request.Unit,
                    Timestamp = DateTime.UtcNow,
                    CorrelationId = correlationId
                };

                await _eventPublisher.PublishAsync(simulatorEvent, SimulatorDataRoutingKey);

                _logger.LogInformation(
                    "Published simulator data: DeviceId={DeviceId}, CorrelationId={CorrelationId}",
                    request.DeviceId, correlationId);

                return Ok(new
                {
                    message = "Simulator data published successfully",
                    correlationId,
                    deviceId = request.DeviceId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to publish simulator data: DeviceId={DeviceId}, CorrelationId={CorrelationId}",
                    request.DeviceId, correlationId);

                return StatusCode(500, new
                {
                    message = "Failed to publish simulator data",
                    correlationId,
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Generates and publishes random simulator data for testing purposes
        /// </summary>
        /// <returns>Result of the publish operation</returns>
        [HttpPost("generate")]
        public async Task<IActionResult> GenerateAndPublishData()
        {
            var random = new Random();
            var correlationId = Guid.NewGuid().ToString();
            var deviceId = Guid.NewGuid().ToString();

            try
            {
                var simulatorEvent = new SimulatorDataEvent
                {
                    DeviceId = deviceId,
                    DeviceName = $"Smart Meter {random.Next(1, 100):D3}",
                    ConsumptionValue = Math.Round(random.NextDouble() * 500, 2),
                    Unit = "kWh",
                    Timestamp = DateTime.UtcNow,
                    CorrelationId = correlationId
                };

                await _eventPublisher.PublishAsync(simulatorEvent, SimulatorDataRoutingKey);

                _logger.LogInformation(
                    "Generated and published simulator data: DeviceId={DeviceId}, ConsumptionValue={ConsumptionValue}, CorrelationId={CorrelationId}",
                    deviceId, simulatorEvent.ConsumptionValue, correlationId);

                return Ok(new
                {
                    message = "Simulator data generated and published successfully",
                    correlationId,
                    data = simulatorEvent
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to generate and publish simulator data: CorrelationId={CorrelationId}",
                    correlationId);

                return StatusCode(500, new
                {
                    message = "Failed to generate and publish simulator data",
                    correlationId,
                    error = ex.Message
                });
            }
        }
    }

    /// <summary>
    /// Request model for publishing simulator data
    /// </summary>
    public class SimulatorDataRequest
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
        public string Unit { get; set; } = "kWh";
    }
}

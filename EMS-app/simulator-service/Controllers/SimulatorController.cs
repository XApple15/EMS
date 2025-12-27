using Microsoft.AspNetCore.Mvc;
using Shared.Events;
using simulator_service.Infrastructure.Messaging;
using simulator_service.Model;
using System.Security.Cryptography;

namespace simulator_service.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class SimulatorController : ControllerBase
    {
        private readonly IEventPublisher _eventPublisher;
        private readonly ILogger<SimulatorController> _logger;

        private const string SimulatorDataRoutingKey = "monitor.data";
        private const string DeviceRoutingKey = "monitoring.device.created";

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
            int dataCount = request.DataCount;

            var random = new Random();

            try
            {
                for (int i = 0; i < dataCount; i++)
                {
                    int value = random.Next(0, 101);

                    var simulatorEvent = new SimulatorDataEvent
                    {
                        DeviceId = request.DeviceId,
                        ConsumptionValue = value,
                        Timestamp = DateTime.UtcNow,
                        CorrelationId = correlationId
                    };

                    await _eventPublisher.PublishAsync(simulatorEvent, SimulatorDataRoutingKey);

                    await Task.Delay(TimeSpan.FromMinutes(10));


                    _logger.LogInformation(
                        "Published simulator data: DeviceId={DeviceId}, CorrelationId={CorrelationId}, Index={Index}",
                        request.DeviceId, correlationId, i);
                }

                return Ok(new
                {
                    message = "Simulator data published successfully",
                    correlationId,
                    deviceId = request.DeviceId,
                    publishedCount = dataCount
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


        [HttpPost("testdevice")]
        public async Task<IActionResult> PublishTestDevice()
        {
            var correlationId = Guid.NewGuid().ToString();
            var deviceid = Guid.NewGuid();

            try
            {
                var deviceEvent = new CreatedDeviceEvent
                {
                    id = deviceid.ToString(),
                    CorrelationId = correlationId
                };

                await _eventPublisher.PublishAsync(deviceEvent, DeviceRoutingKey);

                _logger.LogInformation(
                    "Published test device event: DeviceId={DeviceId}, CorrelationId={CorrelationId}",
                    deviceid, correlationId);

                return Ok(new
                {
                    message = "Test device event published successfully",
                    correlationId,
                    deviceId = deviceid
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to publish test device event: DeviceId={DeviceId}, CorrelationId={CorrelationId}",
                    deviceid, correlationId);

                return StatusCode(500, new
                {
                    message = "Failed to publish test device event",
                    correlationId,
                    error = ex.Message
                });
            }

        }
    }
}
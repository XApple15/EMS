using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using monitoring_service.Data;
using monitoring_service.Infrastructure.Messaging;
using monitoring_service.Model;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace monitoring_service.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EnergyConsumptionController : ControllerBase
    {
        private readonly MonitorDbUtils _context;
        private readonly IEventPublisher _eventPublisher;

        public EnergyConsumptionController(MonitorDbUtils context, IEventPublisher _publisher)
        {
            _context = context;
            _eventPublisher = _publisher;
        }

        /// <summary>
        /// Get hourly energy consumption for a specific device and date
        /// </summary>
        /// <param name="deviceId">The device ID</param>
        /// <param name="date">The date in YYYY-MM-DD format</param>
        /// <returns>Hourly consumption data</returns>
        [HttpGet("{deviceId}")]
        public async Task<IActionResult> GetDailyConsumption(Guid deviceId, [FromQuery] string date)
        {
            // Validate date parameter
            if (string.IsNullOrEmpty(date) || !DateTime.TryParse(date, out DateTime selectedDate))
            {
                return BadRequest(new { error = "Invalid date format.  Use YYYY-MM-DD." });
            }

            try
            {
                var startDate = selectedDate.Date;
                var endDate = startDate.AddDays(1);

                var consumptionData = await _context.Consumptions
                    .Where(c => c.DeviceId == deviceId
                           && c.Timestamp >= startDate
                           && c.Timestamp < endDate)
                    .OrderBy(c => c.Timestamp)
                    .ToListAsync();

                var hourlyData = consumptionData
                    .GroupBy(c => c.Timestamp.Hour)
                    .Select(g => new
                    {
                        hour = g.Key,
                        energyKwh = Math.Round(g.Sum(c => c.ConsumptionValue), 2)
                    })
                    .OrderBy(h => h.hour)
                    .ToList();

                var completeHourlyData = Enumerable.Range(0, 24)
                    .Select(hour => new
                    {
                        hour = hour,
                        energyKwh = hourlyData.FirstOrDefault(h => h.hour == hour)?.energyKwh ?? 0
                    })
                    .ToList();

                var response = new
                {
                    deviceId = deviceId,
                    date = selectedDate.ToString("yyyy-MM-dd"),
                    data = completeHourlyData,
                    totalConsumption = Math.Round(completeHourlyData.Sum(h => h.energyKwh), 2)
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = "An error occurred while fetching consumption data.",
                    details = ex.Message
                });
            }
        }





        [HttpPost]
        public async Task<IActionResult> SendTestNotification([FromBody] Notification notification)
        {
            // This is a placeholder for sending a test notification.
            // In a real implementation, you would publish this to RabbitMQ.

            await _eventPublisher.PublishAsync(notification,"user_notifications");

            return Ok(new
            {
                message = "Test notification sent.",
                notification = notification
            });
        }
    }
}
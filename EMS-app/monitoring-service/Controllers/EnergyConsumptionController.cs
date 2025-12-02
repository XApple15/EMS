using Microsoft.AspNetCore.Mvc;
using monitoring_service.Model;
using System.Globalization;

namespace monitoring_service.Controllers
{
    /// <summary>
    /// Controller for energy consumption monitoring and historical data
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class EnergyConsumptionController : ControllerBase
    {
        private readonly ILogger<EnergyConsumptionController> _logger;

        public EnergyConsumptionController(ILogger<EnergyConsumptionController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Get hourly energy consumption data for a specific client and date
        /// </summary>
        /// <param name="clientId">The client/user ID</param>
        /// <param name="date">The date in YYYY-MM-DD format</param>
        /// <returns>Hourly energy consumption data (24 data points)</returns>
        /// <remarks>
        /// Returns energy consumption data for each hour of the specified day.
        /// 
        /// Sample request:
        /// 
        ///     GET /api/energyconsumption/client/{clientId}?date=2025-12-02
        ///     
        /// Sample response:
        /// 
        ///     {
        ///         "clientId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        ///         "date": "2025-12-02",
        ///         "data": [
        ///             {"hour": 0, "energyKwh": 2.5},
        ///             {"hour": 1, "energyKwh": 2.1},
        ///             ...
        ///             {"hour": 23, "energyKwh": 3.2}
        ///         ]
        ///     }
        ///     
        /// **Authorization**: Clients can only access their own data
        /// </remarks>
        /// <response code="200">Successfully retrieved energy consumption data</response>
        /// <response code="400">Invalid date format or missing required parameters</response>
        /// <response code="401">Unauthorized - client can only access their own data</response>
        [HttpGet("client/{clientId}")]
        [ProducesResponseType(typeof(DailyEnergyConsumptionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public ActionResult<DailyEnergyConsumptionResponse> GetDailyEnergyConsumption(
            string clientId, 
            [FromQuery] string date)
        {
            // Validate clientId
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return BadRequest(new { message = "Client ID is required" });
            }

            // Validate date parameter
            if (string.IsNullOrWhiteSpace(date))
            {
                return BadRequest(new { message = "Date parameter is required (format: YYYY-MM-DD)" });
            }

            // Parse and validate date format
            if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly parsedDate))
            {
                return BadRequest(new { message = "Invalid date format. Please use YYYY-MM-DD format" });
            }

            // Check authorization - ensure client can only access their own data
            var requestedUserId = Request.Headers["X-User-Id"].FirstOrDefault();
            var userRole = Request.Headers["X-User-Role"].FirstOrDefault();

            // Allow access if user is admin or if clientId matches the requesting user's ID
            bool isAdmin = string.Equals(userRole, "admin", StringComparison.OrdinalIgnoreCase);
            bool isOwnData = string.Equals(clientId, requestedUserId, StringComparison.OrdinalIgnoreCase);

            if (!isAdmin && !isOwnData && !string.IsNullOrEmpty(requestedUserId))
            {
                _logger.LogWarning("Unauthorized access attempt: User {UserId} tried to access data for client {ClientId}", 
                    requestedUserId, clientId);
                return Unauthorized(new { message = "You can only access your own energy consumption data" });
            }

            // Generate mock data for demonstration
            // In a real implementation, this would fetch from a database
            var hourlyData = GenerateMockHourlyData(clientId, parsedDate);

            var response = new DailyEnergyConsumptionResponse
            {
                ClientId = clientId,
                Date = date,
                Data = hourlyData
            };

            _logger.LogInformation("Retrieved energy consumption data for client {ClientId} on {Date}", clientId, date);

            return Ok(response);
        }

        /// <summary>
        /// Generates mock hourly energy consumption data for demonstration purposes
        /// </summary>
        private List<HourlyEnergyConsumption> GenerateMockHourlyData(string clientId, DateOnly date)
        {
            var random = new Random(clientId.GetHashCode() + date.DayNumber);
            var hourlyData = new List<HourlyEnergyConsumption>();

            for (int hour = 0; hour < 24; hour++)
            {
                // Generate realistic energy consumption patterns
                // Lower consumption at night (0-6), higher during day (7-22), moderate at evening
                double baseConsumption = hour switch
                {
                    >= 0 and <= 5 => 1.0 + random.NextDouble() * 1.5,   // Night: 1.0-2.5 kWh
                    >= 6 and <= 8 => 2.5 + random.NextDouble() * 2.0,   // Morning: 2.5-4.5 kWh
                    >= 9 and <= 11 => 2.0 + random.NextDouble() * 2.5,  // Late morning: 2.0-4.5 kWh
                    >= 12 and <= 14 => 3.0 + random.NextDouble() * 2.0, // Afternoon: 3.0-5.0 kWh
                    >= 15 and <= 17 => 2.5 + random.NextDouble() * 2.0, // Late afternoon: 2.5-4.5 kWh
                    >= 18 and <= 21 => 4.0 + random.NextDouble() * 3.0, // Evening peak: 4.0-7.0 kWh
                    _ => 2.0 + random.NextDouble() * 2.0                // Late evening: 2.0-4.0 kWh
                };

                hourlyData.Add(new HourlyEnergyConsumption
                {
                    Hour = hour,
                    EnergyKwh = Math.Round(baseConsumption, 2)
                });
            }

            return hourlyData;
        }
    }
}

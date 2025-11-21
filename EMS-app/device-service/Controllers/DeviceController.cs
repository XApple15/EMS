using Microsoft.AspNetCore.Mvc;
using device_service.Model;
using device_service.Interface;

namespace device_service.Controllers
{
    /// <summary>
    /// Device management and energy consumption tracking endpoints
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class DeviceController : ControllerBase
    {
        private readonly IDeviceService _deviceService;

        public DeviceController(IDeviceService deviceService)
        {
            _deviceService = deviceService;
        }

        /// <summary>
        /// Get all devices in the system (Admin only)
        /// </summary>
        /// <returns>List of all devices with their assignments and consumption data</returns>
        /// <remarks>
        /// Retrieves all devices registered in the system, including assigned and unassigned devices.
        /// 
        /// **Authorization**: Requires Admin role
        /// 
        /// The JWT middleware automatically injects X-User-Role header from the token.
        /// </remarks>
        /// <response code="200">Successfully retrieved list of devices</response>
        /// <response code="401">Unauthorized - Admin role required</response>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<Device>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<IEnumerable<Device>>> GetAllDevices()
        {
            var role = Request.Headers["X-User-Role"].FirstOrDefault();

            if (!string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase))
                return Unauthorized(new { message = "Admin role required" });

            var devices = await _deviceService.GetAllDevicesAsync();
            return Ok(devices);
        }

        /// <summary>
        /// Get a specific device by its ID
        /// </summary>
        /// <param name="id">The unique device ID (GUID)</param>
        /// <returns>Device details including name, consumption, and user assignment</returns>
        /// <remarks>
        /// Retrieves a single device by its unique identifier.
        /// 
        /// Sample request:
        /// 
        ///     GET /api/device/3fa85f64-5717-4562-b3fc-2c963f66afa6
        ///     
        /// </remarks>
        /// <response code="200">Device found and returned</response>
        /// <response code="404">Device not found</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(Device), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<Device>> GetDeviceById(Guid id)
        {
            var device = await _deviceService.GetDeviceByIdAsync(id);

            if (device == null)
            {
                return NotFound(new { message = $"Device with ID {id} not found" });
            }

            return Ok(device);
        }

        /// <summary>
        /// Get all devices assigned to a specific user
        /// </summary>
        /// <param name="userId">The user's profile ID (GUID)</param>
        /// <returns>List of devices assigned to the specified user</returns>
        /// <remarks>
        /// Retrieves all devices that are currently assigned to a particular user.
        /// This is useful for client dashboards to show their own devices.
        /// 
        /// Sample request:
        /// 
        ///     GET /api/device/user/3fa85f64-5717-4562-b3fc-2c963f66afa6
        ///     
        /// Returns empty array if user has no devices assigned.
        /// </remarks>
        /// <response code="200">Successfully retrieved user's devices (may be empty array)</response>
        [HttpGet("user/{userId}")]
        [ProducesResponseType(typeof(IEnumerable<Device>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<Device>>> GetDevicesByUserId(Guid userId)
        {
            var devices = await _deviceService.GetDevicesByUserIdAsync(userId);
            return Ok(devices);
        }

        /// <summary>
        /// Create a new device (Admin only)
        /// </summary>
        /// <param name="deviceDto">Device creation data</param>
        /// <returns>Created device with generated ID</returns>
        /// <remarks>
        /// Creates a new device in the system. The device can be created unassigned or assigned to a user.
        /// 
        /// **Authorization**: Requires Admin role
        /// 
        /// Sample request (unassigned device):
        /// 
        ///     POST /api/device
        ///     {
        ///        "userId": null,
        ///        "name": "Smart Thermostat",
        ///        "consumption": "2.5"
        ///     }
        ///     
        /// Sample request (assigned device):
        /// 
        ///     POST /api/device
        ///     {
        ///        "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        ///        "name": "LED Lamp",
        ///        "consumption": "0.8"
        ///     }
        ///     
        /// Consumption is typically measured in kWh (kilowatt-hours).
        /// </remarks>
        /// <response code="201">Device created successfully</response>
        /// <response code="400">Invalid request data</response>
        /// <response code="401">Unauthorized - Admin role required</response>
        [HttpPost]
        [ProducesResponseType(typeof(Device), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<Device>> CreateDevice([FromBody] AddDeviceDTO deviceDto)
        {
            var role = Request.Headers["X-User-Role"].FirstOrDefault();

            if (!string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase))
                return Unauthorized(new { message = "Admin role required" });

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var device = await _deviceService.CreateDeviceAsync(deviceDto);
            return CreatedAtAction(nameof(GetDeviceById), new { id = device.id }, device);
        }

        /// <summary>
        /// Update an existing device or reassign to another user (Admin only)
        /// </summary>
        /// <param name="id">The device ID to update</param>
        /// <param name="deviceDto">Updated device data</param>
        /// <returns>Updated device information</returns>
        /// <remarks>
        /// Updates device details such as name, consumption, or user assignment.
        /// 
        /// **Authorization**: Requires Admin role
        /// 
        /// Sample request (update and reassign):
        /// 
        ///     PUT /api/device/3fa85f64-5717-4562-b3fc-2c963f66afa6
        ///     {
        ///        "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        ///        "name": "Updated Device Name",
        ///        "consumption": "3.2"
        ///     }
        ///     
        /// Sample request (unassign device):
        /// 
        ///     PUT /api/device/3fa85f64-5717-4562-b3fc-2c963f66afa6
        ///     {
        ///        "userId": null,
        ///        "name": "Device Name",
        ///        "consumption": "3.2"
        ///     }
        ///     
        /// </remarks>
        /// <response code="200">Device updated successfully</response>
        /// <response code="400">Invalid request data</response>
        /// <response code="401">Unauthorized - Admin role required</response>
        /// <response code="404">Device not found</response>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(Device), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<Device>> UpdateDevice(Guid id, [FromBody] AddDeviceDTO deviceDto)
        {
            var role = Request.Headers["X-User-Role"].FirstOrDefault();

            if (!string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase))
                return Unauthorized(new { message = "Admin role required" });

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var device = await _deviceService.UpdateDeviceAsync(id, deviceDto);

            if (device == null)
            {
                return NotFound(new { message = $"Device with ID {id} not found" });
            }

            return Ok(device);
        }

        /// <summary>
        /// Delete a device (Admin only)
        /// </summary>
        /// <param name="id">The device ID to delete</param>
        /// <returns>No content on success</returns>
        /// <remarks>
        /// Permanently deletes a device from the system.
        /// 
        /// **Authorization**: Requires Admin role
        /// 
        /// **Warning**: This action cannot be undone. All historical consumption data for this device will be lost.
        /// 
        /// Sample request:
        /// 
        ///     DELETE /api/device/3fa85f64-5717-4562-b3fc-2c963f66afa6
        ///     
        /// </remarks>
        /// <response code="204">Device deleted successfully</response>
        /// <response code="401">Unauthorized - Admin role required</response>
        /// <response code="404">Device not found</response>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> DeleteDevice(Guid id)
        {
            var role = Request.Headers["X-User-Role"].FirstOrDefault();

            if (!string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase))
                return Unauthorized(new { message = "Admin role required" });

            var result = await _deviceService.DeleteDeviceAsync(id);

            if (!result)
            {
                return NotFound(new { message = $"Device with ID {id} not found" });
            }

            return NoContent();
        }
    }
}
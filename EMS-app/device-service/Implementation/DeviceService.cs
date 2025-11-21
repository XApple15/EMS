using device_service.Data;
using device_service.Interface;
using device_service.Model;
using Microsoft.EntityFrameworkCore;

namespace device_service.Implementation
{
    public class DeviceService : IDeviceService
    {
        private readonly DeviceDButils _context;
        private readonly ILogger<DeviceService> _logger;

        public DeviceService(DeviceDButils context, ILogger<DeviceService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<Device>> GetAllDevicesAsync()
        {
            try
            {
                _logger.LogInformation("Fetching all devices");
                return await _context.Devices.ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching all devices");
                throw;
            }
        }

        public async Task<Device?> GetDeviceByIdAsync(Guid id)
        {
            try
            {
                _logger.LogInformation("Fetching device with ID: {DeviceId}", id);
                return await _context.Devices.FindAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching device with ID: {DeviceId}", id);
                throw;
            }
        }

        public async Task<IEnumerable<Device>> GetDevicesByUserIdAsync(Guid userId)
        {
            try
            {
                _logger.LogInformation("Fetching devices for user with ID: {UserId}", userId);
                return await _context.Devices
                    .Where(d => d.userId == userId)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching devices for user with ID: {UserId}", userId);
                throw;
            }
        }

        public async Task<Device> CreateDeviceAsync(AddDeviceDTO deviceDto)
        {
            try
            {
                _logger.LogInformation("Creating new device with name: {DeviceName}", deviceDto.name);

                var device = new Device
                {
                    id = Guid.NewGuid(),
                    userId = deviceDto.userId,
                    name = deviceDto.name,
                    consumption = deviceDto.consumption
                };

                _context.Devices.Add(device);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Device created successfully with ID: {DeviceId}", device.id);
                return device;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating device");
                throw;
            }
        }

        public async Task<Device?> UpdateDeviceAsync(Guid id, AddDeviceDTO deviceDto)
        {
            try
            {
                _logger.LogInformation("Updating device with ID: {DeviceId}", id);

                var device = await _context.Devices.FindAsync(id);

                if (device == null)
                {
                    _logger.LogWarning("Device with ID {DeviceId} not found", id);
                    return null;
                }

                device.userId = deviceDto.userId;
                device.name = deviceDto.name;
                device.consumption = deviceDto.consumption;

                _context.Devices.Update(device);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Device with ID {DeviceId} updated successfully", id);
                return device;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating device with ID: {DeviceId}", id);
                throw;
            }
        }

        public async Task<bool> DeleteDeviceAsync(Guid id)
        {
            try
            {
                _logger.LogInformation("Deleting device with ID: {DeviceId}", id);

                var device = await _context.Devices.FindAsync(id);

                if (device == null)
                {
                    _logger.LogWarning("Device with ID {DeviceId} not found", id);
                    return false;
                }

                _context.Devices.Remove(device);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Device with ID {DeviceId} deleted successfully", id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting device with ID: {DeviceId}", id);
                throw;
            }
        }
    }
}

using device_service.Model;

namespace device_service.Interface
{
    public interface IDeviceService
    {
        Task<IEnumerable<Device>> GetAllDevicesAsync();
        Task<Device?> GetDeviceByIdAsync(Guid id);
        Task<IEnumerable<Device>> GetDevicesByUserIdAsync(Guid userId);
        Task<Device> CreateDeviceAsync(AddDeviceDTO deviceDto);
        Task<Device?> UpdateDeviceAsync(Guid id, AddDeviceDTO deviceDto);
        Task<bool> DeleteDeviceAsync(Guid id);
    }
}

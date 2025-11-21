using System.ComponentModel.DataAnnotations;

namespace device_service.Model
{
    public class AddDeviceDTO
    {
        public Guid? userId { get; set; }
        [Required]
        public string name { get; set; }
        [Required]
        public string consumption { get; set; }
    }
}

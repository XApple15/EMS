using System.ComponentModel.DataAnnotations;

namespace user_service.Model
{
    public class AddUserDTO
    {
        [Required]
        public Guid AuthId { get; set; }
        [Required]
        public string Username { get; set; }
        public string Address { get; set; }
    }
}

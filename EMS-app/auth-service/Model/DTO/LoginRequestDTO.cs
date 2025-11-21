using System.ComponentModel.DataAnnotations;

namespace auth_service.Model.DTO
{
    public class LoginRequestDTO
    {
        [Required]
        public string Email { get; set; }
        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }
}

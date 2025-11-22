using System.ComponentModel.DataAnnotations;

namespace auth_service.Model.DTO
{
    public class RegisterRequestDTO
    {
        [Required]
        [DataType(DataType.EmailAddress)]
        public string Email { get; set; }
        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }
        public string[] Roles { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }
}

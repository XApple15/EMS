namespace auth_service.Model.DTO
{
    public class LoginResponseDTO
    {
        public string JwtToken { get; set; }
        public AuthUserDTO User { get; set; }
        public List<string> Roles { get; set; }
    }
}

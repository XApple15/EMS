using Microsoft.AspNetCore.Identity;

namespace auth_service.Interfaces
{
    public interface ITokenRepository
    {
        string CreateJWTToken(IdentityUser user, List<string> roles);
    }
}

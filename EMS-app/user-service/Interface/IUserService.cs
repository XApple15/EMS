using user_service.Model;

namespace user_service.Interface
{
    public interface IUserService
    {
        Task<IEnumerable<User>> GetAllUsersAsync();
        Task<User?> GetUserByIdAsync(Guid id);
        Task<User?> GetUserByAuthIdAsync(Guid authId);
        Task<User> CreateUserAsync(AddUserDTO userDto);
        Task<User?> UpdateUserAsync(Guid id, AddUserDTO userDto);
        Task<bool> DeleteUserAsync(Guid id);
    }
}

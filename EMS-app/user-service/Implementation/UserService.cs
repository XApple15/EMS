using Microsoft.EntityFrameworkCore;
using user_service.Data;
using user_service.Interface;
using user_service.Model;

namespace user_service.Implementation
{
    public class UserService : IUserService
    {
        private readonly UserDButils _context;
        private readonly ILogger<UserService> _logger;

        public UserService(UserDButils context, ILogger<UserService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<User>> GetAllUsersAsync()
        {
            try
            {
                _logger.LogInformation("Fetching all users");
                return await _context.Users.ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching all users");
                throw;
            }
        }

        public async Task<User?> GetUserByIdAsync(Guid id)
        {
            try
            {
                _logger.LogInformation("Fetching user with ID: {UserId}", id);
                return await _context.Users.FindAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching user with ID: {UserId}", id);
                throw;
            }
        }

        public async Task<User?> GetUserByAuthIdAsync(Guid authId)
        {
            try
            {
                _logger.LogInformation("Fetching user with AuthId: {AuthId}", authId);
                return await _context.Users
                    .FirstOrDefaultAsync(u => u.AuthId == authId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching user with AuthId: {AuthId}", authId);
                throw;
            }
        }

        public async Task<User> CreateUserAsync(AddUserDTO userDto)
        {
            try
            {
                _logger.LogInformation("Creating new user with Username: {Username}", userDto.Username);

                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.AuthId == userDto.AuthId);

                if (existingUser != null)
                {
                    _logger.LogWarning("User with AuthId {AuthId} already exists", userDto.AuthId);
                    throw new InvalidOperationException($"User with AuthId {userDto.AuthId} already exists");
                }

                var user = new User
                {
                    Id = Guid.NewGuid(),
                    AuthId = userDto.AuthId,
                    Username = userDto.Username,
                    Address = userDto.Address
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation("User created successfully with ID: {UserId}", user.Id);
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating user");
                throw;
            }
        }

        public async Task<User?> UpdateUserAsync(Guid id, AddUserDTO userDto)
        {
            try
            {
                _logger.LogInformation("Updating user with ID: {UserId}", id);

                var user = await _context.Users.FindAsync(id);

                if (user == null)
                {
                    _logger.LogWarning("User with ID {UserId} not found", id);
                    return null;
                }

                if (user.AuthId != userDto.AuthId)
                {
                    var existingUser = await _context.Users
                        .FirstOrDefaultAsync(u => u.AuthId == userDto.AuthId && u.Id != id);

                    if (existingUser != null)
                    {
                        _logger.LogWarning("AuthId {AuthId} is already in use", userDto.AuthId);
                        throw new InvalidOperationException($"AuthId {userDto.AuthId} is already in use");
                    }
                }

                user.AuthId = userDto.AuthId;
                user.Username = userDto.Username;
                user.Address = userDto.Address;

                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation("User with ID {UserId} updated successfully", id);
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating user with ID: {UserId}", id);
                throw;
            }
        }

        public async Task<bool> DeleteUserAsync(Guid id)
        {
            try
            {
                _logger.LogInformation("Deleting user with ID: {UserId}", id);

                var user = await _context.Users.FindAsync(id);

                if (user == null)
                {
                    _logger.LogWarning("User with ID {UserId} not found", id);
                    return false;
                }

                _context.Users.Remove(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation("User with ID {UserId} deleted successfully", id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting user with ID: {UserId}", id);
                throw;
            }
        }
    }
}

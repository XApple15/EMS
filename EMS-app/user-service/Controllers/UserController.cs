using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using user_service.Interface;
using user_service.Model;

namespace user_service.Controllers
{
    /// <summary>
    /// User profile management endpoints
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        /// <summary>
        /// Get all users in the system (Admin only)
        /// </summary>
        /// <returns>List of all user profiles</returns>
        /// <remarks>
        /// This endpoint retrieves all user profiles from the database.
        /// 
        /// **Authorization**: Requires Admin role
        /// 
        /// The JWT middleware automatically injects X-User-Role header from the token.
        /// </remarks>
        /// <response code="200">Successfully retrieved list of users</response>
        /// <response code="401">Unauthorized - Missing or invalid token, or insufficient permissions</response>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<User>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<IEnumerable<User>>> GetAllUsers()
        {
            var role = Request.Headers["X-User-Role"].FirstOrDefault();

            if (!string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase))
                return Unauthorized(new { message = "Admin role required" });

            var users = await _userService.GetAllUsersAsync();
            return Ok(users);
        }

        /// <summary>
        /// Get a specific user by their profile ID
        /// </summary>
        /// <param name="id">The unique user profile ID (GUID)</param>
        /// <returns>User profile details</returns>
        /// <remarks>
        /// Retrieves a single user profile by their ID.
        /// 
        /// **Authorization**: Requires Admin or Client role
        /// 
        /// Sample request:
        /// 
        ///     GET /api/user/3fa85f64-5717-4562-b3fc-2c963f66afa6
        ///     
        /// </remarks>
        /// <response code="200">User found and returned</response>
        /// <response code="401">Unauthorized - Missing or invalid token</response>
        /// <response code="404">User not found</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(User), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<User>> GetUserById(Guid id)
        {
            var role = Request.Headers["X-User-Role"].FirstOrDefault();

            if (!string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(role, "client", StringComparison.OrdinalIgnoreCase))
                return Unauthorized(new { message = "Admin or Client role required" });

            var user = await _userService.GetUserByIdAsync(id);

            if (user == null)
            {
                return NotFound(new { message = $"User with ID {id} not found" });
            }

            return Ok(user);
        }

        /// <summary>
        /// Get user profile by their authentication ID (path parameter)
        /// </summary>
        /// <param name="authId">The authentication service user ID (GUID from Identity)</param>
        /// <returns>User profile details</returns>
        /// <remarks>
        /// Retrieves a user profile using their auth service ID (from ASP.NET Identity).
        /// This is useful for linking authentication users with their profiles.
        /// 
        /// **Authorization**: Requires Admin or Client role
        /// 
        /// Sample request:
        /// 
        ///     GET /api/user/auth/3fa85f64-5717-4562-b3fc-2c963f66afa6
        ///     
        /// </remarks>
        /// <response code="200">User found and returned</response>
        /// <response code="401">Unauthorized</response>
        /// <response code="404">User not found</response>
        [HttpGet("auth/{authId}")]
        [ProducesResponseType(typeof(User), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<User>> GetUserByAuthId(Guid authId)
        {
            var role = Request.Headers["X-User-Role"].FirstOrDefault();

            if (!string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(role, "client", StringComparison.OrdinalIgnoreCase))
                return Unauthorized(new { message = "Admin or Client role required" });

            var user = await _userService.GetUserByAuthIdAsync(authId);

            if (user == null)
            {
                return NotFound(new { message = $"User with AuthId {authId} not found" });
            }

            return Ok(user);
        }

        /// <summary>
        /// Get user profile by authentication ID (query parameter)
        /// </summary>
        /// <param name="authId">The authentication service user ID (GUID)</param>
        /// <returns>User profile details</returns>
        /// <remarks>
        /// Alternative endpoint to retrieve user by auth ID using a query parameter.
        /// 
        /// **Authorization**: Requires Admin or Client role
        /// 
        /// Sample request:
        /// 
        ///     GET /api/user/by-auth?authId=3fa85f64-5717-4562-b3fc-2c963f66afa6
        ///     
        /// </remarks>
        /// <response code="200">User found and returned</response>
        /// <response code="400">Invalid or missing authId parameter</response>
        /// <response code="401">Unauthorized</response>
        /// <response code="404">User not found</response>
        [HttpGet("by-auth")]
        [ProducesResponseType(typeof(User), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<User>> GetUserByAuthIdQuery([FromQuery] Guid authId)
        {
            var role = Request.Headers["X-User-Role"].FirstOrDefault();

            if (!string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(role, "client", StringComparison.OrdinalIgnoreCase))
                return Unauthorized(new { message = "Admin or Client role required" });

            if (authId == Guid.Empty)
            {
                return BadRequest(new { message = "Invalid or missing authId query parameter" });
            }

            var user = await _userService.GetUserByAuthIdAsync(authId);

            if (user == null)
            {
                return NotFound(new { message = $"User with AuthId {authId} not found" });
            }

            return Ok(user);
        }

        /// <summary>
        /// Create a new user profile
        /// </summary>
        /// <param name="userDto">User profile creation data</param>
        /// <returns>Created user profile</returns>
        /// <remarks>
        /// Creates a new user profile in the system.
        /// This should be called AFTER creating the user in the auth service.
        /// 
        /// **Note**: This endpoint is typically called by the frontend after successful registration.
        /// 
        /// Sample request:
        /// 
        ///     POST /api/user
        ///     {
        ///        "authId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        ///        "username": "johndoe",
        ///        "address": "123 Main St, City, Country"
        ///     }
        ///     
        /// </remarks>
        /// <response code="201">User profile created successfully</response>
        /// <response code="400">Invalid request data</response>
        [HttpPost]
        [ProducesResponseType(typeof(User), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<User>> CreateUser([FromBody] AddUserDTO userDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userService.CreateUserAsync(userDto);
            return CreatedAtAction(nameof(GetUserById), new { id = user.Id }, user);
        }

        /// <summary>
        /// Update an existing user profile
        /// </summary>
        /// <param name="id">The user profile ID to update</param>
        /// <param name="userDto">Updated user profile data</param>
        /// <returns>Updated user profile</returns>
        /// <remarks>
        /// Updates user profile information such as username and address.
        /// 
        /// **Note**: The AuthId cannot be changed.
        /// 
        /// Sample request:
        /// 
        ///     PUT /api/user/3fa85f64-5717-4562-b3fc-2c963f66afa6
        ///     {
        ///        "authId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        ///        "username": "johnupdated",
        ///        "address": "456 New St, City, Country"
        ///     }
        ///     
        /// </remarks>
        /// <response code="200">User profile updated successfully</response>
        /// <response code="400">Invalid request data</response>
        /// <response code="404">User not found</response>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(User), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<User>> UpdateUser(Guid id, [FromBody] AddUserDTO userDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userService.UpdateUserAsync(id, userDto);

            if (user == null)
            {
                return NotFound(new { message = $"User with ID {id} not found" });
            }

            return Ok(user);
        }

        /// <summary>
        /// Delete a user profile (Admin only)
        /// </summary>
        /// <param name="id">The user profile ID to delete</param>
        /// <returns>No content on success</returns>
        /// <remarks>
        /// Permanently deletes a user profile from the database.
        /// 
        /// **Authorization**: Requires Admin role
        /// 
        /// **Important**: This only deletes the profile. The auth service user should be deleted separately
        /// or use the composite delete endpoint in the frontend api-service.
        /// 
        /// Sample request:
        /// 
        ///     DELETE /api/user/3fa85f64-5717-4562-b3fc-2c963f66afa6
        ///     
        /// </remarks>
        /// <response code="204">User profile deleted successfully</response>
        /// <response code="401">Unauthorized - Admin role required</response>
        /// <response code="404">User not found</response>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> DeleteUser(Guid id)
        {
            var role = Request.Headers["X-User-Role"].FirstOrDefault();

            if (!string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase))
                return Unauthorized(new { message = "Admin role required" });

            var result = await _userService.DeleteUserAsync(id);

            if (!result)
            {
                return NotFound(new { message = $"User with ID {id} not found" });
            }

            return NoContent();
        }
    }
}
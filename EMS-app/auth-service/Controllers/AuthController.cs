using auth_service.Data;
using auth_service.Infrastructure.Messaging;
using auth_service.Interfaces;
using auth_service.Model.Domain;
using auth_service.Model.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Events;
using System.Security.Claims;

namespace auth_service.Controllers
{
    /// <summary>
    /// Authentication and user management endpoints
    /// </summary>
    [Route("[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITokenRepository _tokenRepository;
        private readonly AuthDButils _dbContext;
        private readonly IEventPublisher _eventPublisher;
        private readonly ILogger<AuthController> _logger;

        public AuthController(UserManager<ApplicationUser> userManager,
                              ITokenRepository tokenRepository,
                              AuthDButils dbContext,
                              IEventPublisher eventPublisher,
                              ILogger<AuthController> logger)
        {
            _userManager = userManager;
            _tokenRepository = tokenRepository;
            _dbContext = dbContext;
            _eventPublisher = eventPublisher;
            _logger = logger;
        }

        /// <summary>
        /// Register a new user account
        /// </summary>
        /// <param name="registerRequestDTO">User registration details including email, password, and roles</param>
        /// <returns>Created user information with ID</returns>
        /// <remarks>
        /// Sample request:
        /// 
        ///     POST /Auth/Register
        ///     {
        ///        "email": "user@example.com",
        ///        "password": "SecurePassword123",
        ///        "roles": ["Client"]
        ///     }
        ///     
        /// Available roles: Admin, Client
        /// </remarks>
        /// <response code="200">User created successfully</response>
        /// <response code="400">Invalid input or registration failed</response>
        /// <response code="500">Internal server error during registration</response>
        [HttpPost("Register")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDTO registerRequestDTO)
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                var identityUser = new ApplicationUser
                {
                    Email = registerRequestDTO.Email,
                    UserName = registerRequestDTO.Email
                };

                var identityResult = await _userManager.CreateAsync(identityUser, registerRequestDTO.Password);
                if (!identityResult.Succeeded)
                    return BadRequest(new { message = "User registration failed", errors = identityResult.Errors });

                if (registerRequestDTO.Roles != null && registerRequestDTO.Roles.Any())
                {
                    identityResult = await _userManager.AddToRolesAsync(identityUser, registerRequestDTO.Roles);
                    if (!identityResult.Succeeded)
                        return BadRequest(new { message = "Role assignment failed", errors = identityResult.Errors });
                }

                await transaction.CommitAsync();

                // Generate correlation ID for tracing
                var correlationId = Guid.NewGuid().ToString();

                // Publish UserRegistered event
                try
                {
                    var userRegisteredEvent = new UserRegisteredEvent
                    {
                        UserId = identityUser.Id,
                        Email = identityUser.Email ?? string.Empty,
                        Username = identityUser.UserName ?? string.Empty,
                        FirstName = registerRequestDTO.FirstName ?? string.Empty,
                        LastName = registerRequestDTO.LastName ?? string.Empty,
                        RegisteredAt = DateTime.UtcNow,
                        CorrelationId = correlationId
                    };

                    await _eventPublisher.PublishAsync(userRegisteredEvent, "user.registered");

                    _logger.LogInformation(
                        "User registered and event published: UserId={UserId}, CorrelationId={CorrelationId}",
                        identityUser.Id, correlationId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, 
                        "Failed to publish UserRegistered event for user {UserId}, CorrelationId={CorrelationId}",
                        identityUser.Id, correlationId);
                    // Continue - user is created but event publishing failed
                }

                return Ok(new
                {
                    message = "User created successfully",
                    id = identityUser.Id,
                    correlationId = correlationId
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = $"Registration failed: {ex.Message}" });
            }
        }

        /// <summary>
        /// Login and receive JWT authentication token
        /// </summary>
        /// <param name="loginRequestDTO">Login credentials (email and password)</param>
        /// <returns>JWT token, user information, and assigned roles</returns>
        /// <remarks>
        /// Sample request:
        /// 
        ///     POST /Auth/Login
        ///     {
        ///        "email": "user@example.com",
        ///        "password": "SecurePassword123"
        ///     }
        ///     
        /// The returned JWT token should be included in subsequent requests:
        /// Authorization: Bearer {token}
        /// </remarks>
        /// <response code="200">Login successful, returns JWT token and user info</response>
        /// <response code="400">Invalid email or password</response>
        /// <response code="500">Internal server error during login</response>
        [HttpPost("Login")]
        [ProducesResponseType(typeof(LoginResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Login([FromBody] LoginRequestDTO loginRequestDTO)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(loginRequestDTO.Email);
                if (user == null)
                    return BadRequest(new { message = "Invalid email or password" });

                var passwordValid = await _userManager.CheckPasswordAsync(user, loginRequestDTO.Password);
                if (!passwordValid)
                    return BadRequest(new { message = "Invalid email or password" });

                var roles = await _userManager.GetRolesAsync(user);

                var jwtToken = _tokenRepository.CreateJWTToken(user, roles.ToList());

                return Ok(new LoginResponseDTO
                {
                    JwtToken = jwtToken,
                    User = new AuthUserDTO
                    {
                        Email = user.Email,
                        Id = user.Id
                    },
                    Roles = roles.ToList()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Login failed: {ex.Message}" });
            }
        }



        /// <summary>
        /// Delete a user account by ID (Admin only)
        /// </summary>
        /// <param name="id">The unique identifier of the user to delete</param>
        /// <returns>Deletion confirmation</returns>
        /// <remarks>
        /// This endpoint permanently deletes a user account from the authentication system.
        /// Typically should only be called after the user profile has been deleted from the user service.
        /// </remarks>
        /// <response code="200">User deleted successfully</response>
        /// <response code="400">Invalid user ID</response>
        /// <response code="404">User not found</response>
        /// <response code="500">Failed to delete user</response>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest(new { message = "Invalid user id" });

            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                    return NotFound(new { message = "User not found" });

                var result = await _userManager.DeleteAsync(user);
                if (!result.Succeeded)
                {
                    var errors = result.Errors.Select(e => e.Description).ToList();
                    return StatusCode(500, new { message = "Failed to delete user", errors });
                }

                await transaction.CommitAsync();

                return Ok(new { message = "User deleted successfully", id = id });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = $"Delete failed: {ex.Message}" });
            }
        }
    }
}
using Asp.Versioning;
using CDRS.Web.Auth;
using Microsoft.AspNetCore.Mvc;

namespace CDRS.Web.Controllers
{
    [ApiVersionNeutral]
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private static readonly string[] ValidRoles =
            { Roles.Worker, Roles.Supervisor, Roles.ProjectManager };

        private readonly TokenService _tokenService;
        private readonly JwtSettings _jwtSettings;

        public AuthController(TokenService tokenService, JwtSettings jwtSettings)
        {
            _tokenService = tokenService;
            _jwtSettings = jwtSettings;
        }

        /// <summary>
        /// Issues a JWT token for demo purposes.
        /// In production, credentials would be validated against a user store.
        /// </summary>
        [HttpPost("token")]
        public IActionResult GetToken([FromBody] TokenRequest request)
        {
            // Demo only: accept any username with a valid role
            // Production would validate credentials against a database
            if (string.IsNullOrWhiteSpace(request.Username))
                return BadRequest(new { error = "Username is required." });

            if (!ValidRoles.Contains(request.Role))
                return BadRequest(new { error = $"Role must be one of: {string.Join(", ", ValidRoles)}" });

            var token = _tokenService.GenerateToken(request.Username, request.Role);

            return Ok(new
            {
                token,
                expires_in = _jwtSettings.ExpiryMinutes * 60,
                token_type = "Bearer"
            });
        }
    }

    public record TokenRequest(string Username, string Role);
}

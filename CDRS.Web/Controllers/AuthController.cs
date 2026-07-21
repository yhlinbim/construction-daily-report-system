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
        private readonly TokenService _tokenService;

        public AuthController(TokenService tokenService)
        {
            _tokenService = tokenService;
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
            var validRoles = new[] { "Worker", "Supervisor", "ProjectManager" };

            if (string.IsNullOrWhiteSpace(request.Username))
                return BadRequest(new { error = "Username is required." });

            if (!validRoles.Contains(request.Role))
                return BadRequest(new { error = $"Role must be one of: {string.Join(", ", validRoles)}" });

            var token = _tokenService.GenerateToken(request.Username, request.Role);

            return Ok(new
            {
                token,
                expires_in = 60,
                token_type = "Bearer"
            });
        }
    }

    public record TokenRequest(string Username, string Role);
}

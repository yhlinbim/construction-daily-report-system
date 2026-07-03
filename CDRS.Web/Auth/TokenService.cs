using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace CDRS.Web.Auth
{
    public class TokenService
    {
        private readonly JwtSettings _jwtSettings;

        public TokenService(JwtSettings jwtSettings)
        {
            _jwtSettings = jwtSettings;
        }

        /// <summary>
        /// Generates a JWT token for the specified user and role.
        /// Token is signed with HMAC-SHA256 and expires after the configured duration.
        /// </summary>
        public string GenerateToken(string username, string role)
        {
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));

            var credentials = new SigningCredentials(
                key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti,
                Guid.NewGuid().ToString())
        };

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}

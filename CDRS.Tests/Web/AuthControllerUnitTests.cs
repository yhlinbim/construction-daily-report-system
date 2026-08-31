using System.Text.Json;
using CDRS.Web.Auth;
using CDRS.Web.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace CDRS.Tests.Web
{
    /// <summary>
    /// Unit tests for AuthController — the demo token endpoint.
    /// </summary>
    public class AuthControllerUnitTests
    {
        private static AuthController CreateController(int expiryMinutes)
        {
            var settings = new JwtSettings
            {
                SecretKey = "unit-test-signing-key-minimum-32-characters",
                Issuer = "cdrs-poc",
                Audience = "cdrs-api",
                ExpiryMinutes = expiryMinutes
            };
            return new AuthController(new TokenService(settings), settings);
        }

        [Fact]
        public void GetToken_WithValidRequest_ReturnsExpiresInMatchingLifetime()
        {
            var controller = CreateController(expiryMinutes: 45);

            var result = controller.GetToken(new TokenRequest("alice", Roles.Worker));

            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            var json = JsonSerializer.SerializeToElement(ok.Value);
            json.GetProperty("expires_in").GetInt32().Should().Be(45 * 60);
            json.GetProperty("token_type").GetString().Should().Be("Bearer");
        }

        [Fact]
        public void GetToken_WithBlankUsername_ReturnsBadRequest()
        {
            var controller = CreateController(expiryMinutes: 60);

            var result = controller.GetToken(new TokenRequest("  ", Roles.Worker));

            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public void GetToken_WithUnknownRole_ReturnsBadRequest()
        {
            var controller = CreateController(expiryMinutes: 60);

            var result = controller.GetToken(new TokenRequest("alice", "Administrator"));

            result.Should().BeOfType<BadRequestObjectResult>();
        }
    }
}

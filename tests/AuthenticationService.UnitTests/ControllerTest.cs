using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Authentication_Service.Controllers;
using Authentication_Service.Business.Interfaces;
using Authentication_Service.DTOs;
using Authentication_Service.Models;
using System;
using System.Security.Claims;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuthenticationService.UnitTests
{
    public class ControllerTests
    {
        private readonly Mock<IAuthService> _authServiceMock = new();
        private readonly AuthController _controller;

        public ControllerTests()
        {
            _controller = new AuthController(_authServiceMock.Object);
        }

        [Fact]
        public async Task Signup_WithNullDto_ReturnsBadRequest()
        {
            var result = await _controller.Signup(null);
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("User data is required", badRequest.Value);
        }

        [Fact]
        public async Task Signup_WithInvalidModelState_ReturnsBadRequest()
        {
            _controller.ModelState.AddModelError("Email", "Required");
            var dto = new UserSignupDto();
            var result = await _controller.Signup(dto);
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.IsType<SerializableError>(badRequest.Value);
        }

        [Fact]
        public async Task Signup_WithEmailAlreadyRegistered_ReturnsBadRequest()
        {
            var dto = new UserSignupDto { Email = "test@example.com", Password = "pass", Username = "test" };
            _authServiceMock.Setup(s => s.RegisterAsync(dto)).ThrowsAsync(new InvalidOperationException("Email is already registered"));

            var result = await _controller.Signup(dto);
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Email is already registered", badRequest.Value);
        }

        [Fact]
        public async Task Signup_WithMissingFields_ThrowsArgumentException_ReturnsBadRequest()
        {
            var dto = new UserSignupDto { Email = null, Password = null, Username = "test" };
            _authServiceMock.Setup(s => s.RegisterAsync(dto)).ThrowsAsync(new ArgumentException("Email is required"));

            var result = await _controller.Signup(dto);
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Email is required", badRequest.Value);
        }

        [Fact]
        public async Task Signup_WithUnexpectedException_ReturnsStatus500()
        {
            var dto = new UserSignupDto { Email = "test@example.com", Password = "pass", Username = "test" };
            _authServiceMock.Setup(s => s.RegisterAsync(dto)).ThrowsAsync(new Exception("Something failed"));

            var result = await _controller.Signup(dto);
            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, status.StatusCode);
            Assert.Equal("Something failed", status.Value);
        }

        [Fact]
        public async Task Signup_WithValidDto_ReturnsOk()
        {
            var dto = new UserSignupDto { Email = "test@example.com", Password = "pass", Username = "test" };
            var expectedUser = new User { Id = 1, Email = dto.Email };

            _authServiceMock.Setup(s => s.RegisterAsync(dto)).ReturnsAsync(expectedUser);

            var result = await _controller.Signup(dto);
            var ok = Assert.IsType<OkObjectResult>(result);
            var returnedUser = Assert.IsType<User>(ok.Value);
            Assert.Equal(expectedUser.Email, returnedUser.Email);
        }

        [Fact]
        public async Task Login_WithNullDto_ReturnsBadRequest()
        {
            var result = await _controller.Login(null);
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Login data is missing.", badRequest.Value);
        }

        [Fact]
        public async Task Login_WithInvalidModelState_ReturnsBadRequest()
        {
            _controller.ModelState.AddModelError("Password", "Required");
            var dto = new UserLoginDto();
            var result = await _controller.Login(dto);
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.IsType<SerializableError>(badRequest.Value);
        }

        [Fact]
        public async Task Login_WithUnauthorizedAccess_ReturnsUnauthorized()
        {
            var dto = new UserLoginDto { Email = "test@example.com", Password = "wrongpass" };
            _authServiceMock.Setup(s => s.LoginAsync(dto)).ThrowsAsync(new UnauthorizedAccessException("Invalid credentials"));

            var result = await _controller.Login(dto);
            var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Equal("Invalid credentials", unauthorized.Value);
        }

        [Fact]
        public async Task Login_WithUnexpectedError_Returns500()
        {
            var dto = new UserLoginDto { Email = "test@example.com", Password = "somepass" };
            _authServiceMock.Setup(s => s.LoginAsync(dto)).ThrowsAsync(new Exception("DB error"));

            var result = await _controller.Login(dto);
            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, status.StatusCode);
            Assert.Contains("DB error", status.Value.ToString());
        }

        [Fact]
        public async Task Login_WithValidDto_ReturnsToken()
        {
            var dto = new UserLoginDto { Email = "test@example.com", Password = "validpass" };
            var token = "jwt_token_here";

            _authServiceMock.Setup(s => s.LoginAsync(dto)).ReturnsAsync(token);

            var result = await _controller.Login(dto);
            var ok = Assert.IsType<OkObjectResult>(result);
            var value = ok.Value!;
            var tokenValue = value.GetType().GetProperty("Token")?.GetValue(value, null);
            Assert.Equal(token, tokenValue);

        }

        [Fact]
        public void Profile_WithAuthenticatedUser_ReturnsUserClaims()
        {
            var controller = new AuthController(_authServiceMock.Object);
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(ClaimTypes.Email, "user@example.com"),
                new Claim(ClaimTypes.Name, "User")
            };
            var identity = new ClaimsIdentity(claims, "Test");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            };

            var result = controller.Profile();
            var ok = Assert.IsType<OkObjectResult>(result);
            var value = ok.Value!;
            var userId = value.GetType().GetProperty("userId")?.GetValue(value, null);
            var username = value.GetType().GetProperty("username")?.GetValue(value, null);
            var email = value.GetType().GetProperty("email")?.GetValue(value, null);

            Assert.Equal("1", userId);
            Assert.Equal("User", username);
            Assert.Equal("user@example.com", email);

        }
    }
}

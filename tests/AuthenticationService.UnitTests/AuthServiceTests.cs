using Authentication_Service.Business;
using Authentication_Service.Business.Interfaces;
using Authentication_Service.Business.Services;
using Authentication_Service.Data;
using Authentication_Service.DTOs;
using Authentication_Service.Events;
using Authentication_Service.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.InMemory;
using Microsoft.Extensions.Configuration;
using Moq;
using System;
using Xunit;
namespace AuthenticationService.UnitTests
{
    public class AuthServiceTests
    {
        private readonly AuthService _authService;
        private readonly AuthDbContext _context;
        private readonly Mock<IConfiguration> _configMock = new();
        private readonly Mock<IRabbitMqPublisher> _rabbitMock = new();

        public AuthServiceTests()
        {
            var options = new DbContextOptionsBuilder<AuthDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AuthDbContext(options);

            _configMock.Setup(c => c["Jwt:Key"]).Returns("this_is_a_very_secure_jwt_key_1234567890");
            _configMock.Setup(c => c["Jwt:Issuer"]).Returns("test_issuer");
            _configMock.Setup(c => c["Jwt:Audience"]).Returns("test_audience");

            _authService = new AuthService(_context, _configMock.Object, _rabbitMock.Object);
        }

        [Fact]
        public async Task RegisterAsync_WithValidInput_ShouldCreateUserAndPublishEvent()
        {
            var dto = new UserSignupDto
            {
                Email = "newuser@example.com",
                Password = "Password123!",
                Username = "NewUser"
            };

            var user = await _authService.RegisterAsync(dto);

            Assert.NotNull(user);
            Assert.Equal(dto.Email, user.Email);
            Assert.NotNull(user.PasswordHash);
            _rabbitMock.Verify(p => p.PublishUserCreatedAsync(It.IsAny<UserCreatedEvent>()), Times.Once);
        }

        [Fact]
        public async Task RegisterAsync_WithExistingEmail_ShouldThrowInvalidOperationException()
        {
            _context.Users.Add(new User
            {
                Email = "duplicate@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!")
            });
            await _context.SaveChangesAsync();

            var dto = new UserSignupDto
            {
                Email = "duplicate@example.com",
                Password = "NewPassword123!",
                Username = "DupUser"
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() => _authService.RegisterAsync(dto));
        }

        [Fact]
        public async Task LoginAsync_WithValidCredentials_ShouldReturnToken()
        {
            var password = "SecurePass123!";
            var email = "validuser@example.com";
            _context.Users.Add(new User
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
            });
            await _context.SaveChangesAsync();

            var dto = new UserLoginDto
            {
                Email = email,
                Password = password
            };

            var token = await _authService.LoginAsync(dto);

            Assert.False(string.IsNullOrWhiteSpace(token));
        }

        [Fact]
        public async Task LoginAsync_WithInvalidPassword_ShouldThrowUnauthorized()
        {
            var email = "wrongpass@example.com";
            _context.Users.Add(new User
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword123!")
            });
            await _context.SaveChangesAsync();

            var dto = new UserLoginDto
            {
                Email = email,
                Password = "WrongPassword"
            };

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _authService.LoginAsync(dto));
        }

        [Fact]
        public async Task LoginAsync_WithNonexistentUser_ShouldThrowUnauthorized()
        {
            var dto = new UserLoginDto
            {
                Email = "nosuchuser@example.com",
                Password = "AnyPassword123!"
            };

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _authService.LoginAsync(dto));
        }

        [Fact]
        public async Task LoginAsync_WithMissingJwtKey_ShouldThrowException()
        {
            var email = "jwtmissing@example.com";
            var password = "SomePassword!";
            _context.Users.Add(new User
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
            });
            await _context.SaveChangesAsync();

            _configMock.Setup(c => c["Jwt:Key"]).Returns((string)null);

            var dto = new UserLoginDto
            {
                Email = email,
                Password = password
            };

            await Assert.ThrowsAsync<Exception>(() => _authService.LoginAsync(dto));
        }
    }
}
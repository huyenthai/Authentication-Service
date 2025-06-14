using Authentication_Service.Business.Interfaces;
using Authentication_Service.Data;
using Authentication_Service.DTOs;
using Authentication_Service.Events;
using Authentication_Service.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Security.Claims;
using System.Text;

namespace Authentication_Service.Business.Services
{
    public class AuthService : IAuthService
    {
        private readonly AuthDbContext context;
        private readonly IConfiguration config;
        private readonly IRabbitMqPublisher rabbitMqPublisher;

        public AuthService(AuthDbContext context, IConfiguration config, IRabbitMqPublisher rabbitMqPublisher)
        {
            this.context = context;
            this.config = config;
            this.rabbitMqPublisher = rabbitMqPublisher;

        }
        public async Task<User> RegisterAsync(UserSignupDto dto)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }
            var existing = await context.Users.AnyAsync(u => u.Email == dto.Email);
            if (existing)
            {
                throw new InvalidOperationException("Email is already registered");
            }
            var user = new User
            {
                Email = dto.Email ?? throw new ArgumentException("Email is required"),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password ?? throw new ArgumentException("Password is required"))
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();
            try
            {
                await rabbitMqPublisher.PublishUserCreatedAsync(new UserCreatedEvent
                {
                    UserId = user.Id,
                    Username = dto.Username ?? "Unknown",
                    Email = user.Email
                });
            }
            catch (Exception ex)
            {
               
                Console.WriteLine($"[Warning] RabbitMQ publish failed: {ex.Message}");
            }

            return user;
        }
        public async Task<string> LoginAsync(UserLoginDto dto)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null || string.IsNullOrWhiteSpace(dto.Password) || !BCrypt.Net.BCrypt.Verify(dto.Password ?? "", user.PasswordHash))
            {
                throw new UnauthorizedAccessException("Invalid credentials");
            }
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email)
            };
            //Using Trim(_) to remove any leading or trailing whitespace characters from the JWT secret key string.
            var jwtKey = config["Jwt:Key"]?.Trim();
            if (string.IsNullOrWhiteSpace(jwtKey))
                throw new Exception("JWT secret key is missing!");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: config["Jwt:Issuer"],
                audience: config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddHours(3),
                signingCredentials: creds);
             return new JwtSecurityTokenHandler().WriteToken(token);
           
        }


    }
}

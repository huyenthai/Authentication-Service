using Authentication_Service.Business.Interfaces;
using Authentication_Service.Data;
using Authentication_Service.DTOs;
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
        private readonly IHttpClientFactory httpClientFactory;

        public AuthService(AuthDbContext context, IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            this.context = context;
            this.config = config;
            this.httpClientFactory = httpClientFactory;
        }
        public async Task<User> RegisterAsync(UserSignupDto dto)
        {
            var existing = await context.Users.AnyAsync(u => u.Email == dto.Email);
            if (existing)
            {
                throw new Exception("Email already in use");
            }
            var user = new User
            {
                Username = dto.Username,
                Email = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();
            //var client = httpClientFactory.CreateClient();
            //var response = await client.PostAsJsonAsync("http://userservice/api/users/create", new
            //{
            //    userId = user.Id,
            //    username = user.Username,
            //    email = user.Email
            //});

            //if (!response.IsSuccessStatusCode)
            //{
            //    throw new Exception("Failed to sync user with UserService");
            //}
                return user;
        }
        public async Task<string> LoginAsync(UserLoginDto dto)
        {
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            {
                throw new Exception("Invalid credentials");
            }
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
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

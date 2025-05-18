using Authentication_Service.Business.Interfaces;
using Authentication_Service.DTOs;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Authentication_Service.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController: ControllerBase
    {
        private readonly IAuthService authService;
        public AuthController(IAuthService authService)
        {
            this.authService = authService;
        }
        [HttpPost("signup")]
        public async Task<IActionResult> Signup(UserSignupDto dto)
        {
          var user = await authService.RegisterAsync(dto);
          return Ok(user);
        }
        [HttpPost("login")]
        public async Task<IActionResult> Login(UserLoginDto dto)
        {
            var token = await authService.LoginAsync(dto);
            return Ok(new { Token = token });
        }

        [HttpGet("profile")]
        [Authorize]
        public IActionResult Profile()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var username = User.FindFirst(ClaimTypes.Name)?.Value;
            var email = User.FindFirst(ClaimTypes.Email)?.Value;

            return Ok(new { userId, username, email });
        }
    }
}

using Authentication_Service.DTOs;
using Authentication_Service.Models;

namespace Authentication_Service.Business.Interfaces
{
    public interface IAuthService
    {
        Task<User> RegisterAsync(UserSignupDto dto);
        Task<String> LoginAsync(UserLoginDto dto);
    }
}

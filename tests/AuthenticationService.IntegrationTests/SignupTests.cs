using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace AuthenticationService.IntegrationTests
{
    public class SignupTests
    {
        private readonly HttpClient _client = new();
        private readonly string _authServiceBaseUrl;
        private readonly string _userServiceBaseUrl;
        private readonly ITestOutputHelper _output;

        private record LoginResponse(string Token);

        public SignupTests(ITestOutputHelper output)
        {
            _output = output;
            _authServiceBaseUrl = Environment.GetEnvironmentVariable("AUTH_SERVICE_URL") ?? "http://auth-service:8080";
            _userServiceBaseUrl = Environment.GetEnvironmentVariable("USER_SERVICE_URL") ?? "http://user-service:8080";
        }


        [Fact]
        public async Task Signup_ThenLogin_ThenGetUserById_ShouldSucceed()
        {
            // Step 1: Signup
            var username = "Testuser" + Guid.NewGuid().ToString("N")[..6];
            var email = $"{username}@example.com";
            var password = "Test123!";

            var signupPayload = new { Username = username, Email = email, Password = password };
            var signupContent = new StringContent(JsonSerializer.Serialize(signupPayload), Encoding.UTF8, "application/json");
            var signupResponse = await _client.PostAsync($"{_authServiceBaseUrl}/api/auth/signup", signupContent);
            signupResponse.EnsureSuccessStatusCode();

            // Step 2: Login to get JWT token
            var loginPayload = new { Email = email, Password = password };
            var loginContent = new StringContent(JsonSerializer.Serialize(loginPayload), Encoding.UTF8, "application/json");
            var loginResponse = await _client.PostAsync($"{_authServiceBaseUrl}/api/auth/login", loginContent);
            if (!loginResponse.IsSuccessStatusCode)
            {
                var errorBody = await loginResponse.Content.ReadAsStringAsync();
                _output.WriteLine($"Login failed with status {loginResponse.StatusCode}: {errorBody}");
                Console.WriteLine($"Login failed with status {loginResponse.StatusCode}: {errorBody}");
            }
            loginResponse.EnsureSuccessStatusCode();


            var loginResult = await JsonSerializer.DeserializeAsync<LoginResponse>(
                await loginResponse.Content.ReadAsStreamAsync(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Assert.NotNull(loginResult?.Token);

            // Step 3: Extract userId from token
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(loginResult.Token);
            var userIdClaim = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub");
            Assert.NotNull(userIdClaim);

            int userId = int.Parse(userIdClaim!.Value);

            _output.WriteLine($"Using AUTH_SERVICE_URL: {_authServiceBaseUrl}");
            _output.WriteLine($"Using USER_SERVICE_URL: {_userServiceBaseUrl}");
            _output.WriteLine($"Signup successful: {email}");
            _output.WriteLine($"JWT: {loginResult.Token}");
            _output.WriteLine($"Extracted userId: {userId}");

            // Step 4: Use token to access user-service
            using var userClient = new HttpClient();
            userClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResult.Token);

            HttpResponseMessage userResponse = null;
            string responseBody = "";

            for (int attempt = 1; attempt <= 5; attempt++)
            {
                userResponse = await userClient.GetAsync($"{_userServiceBaseUrl}/api/user/{userId}");
                if (userResponse.IsSuccessStatusCode)
                {
                    responseBody = await userResponse.Content.ReadAsStringAsync();
                    break;
                }

                var error = await userResponse.Content.ReadAsStringAsync();
                _output.WriteLine($"Attempt {attempt}: User not found. Status: {userResponse.StatusCode}. Body: {error}");
                await Task.Delay(2000);
            }

            userResponse.EnsureSuccessStatusCode();
            _output.WriteLine($"User-service response: {responseBody}");
            Assert.Contains(username, responseBody);

        }

        [Fact]
        public async Task GetProfile_WithoutToken_ShouldReturnUnauthorized()
        {
            using var client = new HttpClient();
            var response = await client.GetAsync($"{_authServiceBaseUrl}/api/auth/profile");

            _output.WriteLine($"Profile request without token returned: {response.StatusCode}");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetProfile_WithValidToken_ShouldReturnUserInfo()
        {
            var email = $"testuser_{Guid.NewGuid()}@example.com";
            var password = "Test123!";
            var username = "TestUser";

            // 1. Signup
            var signupPayload = new
            {
                Email = email,
                Password = password,
                Username = username
            };
            var signupContent = new StringContent(JsonSerializer.Serialize(signupPayload), Encoding.UTF8, "application/json");
            var signupResponse = await _client.PostAsync($"{_authServiceBaseUrl}/api/auth/signup", signupContent);
            signupResponse.EnsureSuccessStatusCode();

            // 2. Login
            var loginPayload = new { Email = email, Password = password };
            var loginContent = new StringContent(JsonSerializer.Serialize(loginPayload), Encoding.UTF8, "application/json");
            var loginResponse = await _client.PostAsync($"{_authServiceBaseUrl}/api/auth/login", loginContent);
            loginResponse.EnsureSuccessStatusCode();

            var loginBody = await loginResponse.Content.ReadAsStringAsync();
            var token = JsonDocument.Parse(loginBody).RootElement.GetProperty("token").GetString();

            // 3. Call /profile
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_authServiceBaseUrl}/api/auth/profile");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var profileResponse = await _client.SendAsync(request);
            profileResponse.EnsureSuccessStatusCode();

            var profileBody = await profileResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Profile body: {profileBody}");

            var json = JsonDocument.Parse(profileBody).RootElement;
            Assert.Equal(email, json.GetProperty("email").GetString());
            Assert.NotNull(json.GetProperty("userId").GetString());
        }


    }
}

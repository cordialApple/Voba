using Voba.Interfaces;
using Voba.Models;

namespace Voba.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IAuthDataRepository _authDataRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IJwtService _jwtService;

        public AuthService(
            IUserRepository userRepository,
            IAuthDataRepository authDataRepository,
            IPasswordHasher passwordHasher,
            IJwtService jwtService)
        {
            _userRepository     = userRepository;
            _authDataRepository = authDataRepository;
            _passwordHasher     = passwordHasher;
            _jwtService         = jwtService;
        }

        /// <summary>Registers a new user and returns the created user on success.</summary>
        public async Task<ServiceResult<User>> RegisterAsync(string email, string username, string password)
        {
            var existing = await _userRepository.GetByEmailAsync(email);
            if (existing is not null)
                return ServiceResult<User>.Fail(ErrorCodes.ValidationError, "Email already registered.");

            var user = new User(email, username, 0);
            await _userRepository.SaveAsync(user);

            var authData = new AuthData(user.Id);
            authData.SetPassword(password, _passwordHasher);
            await _authDataRepository.SaveAsync(authData);

            return ServiceResult<User>.Ok(user);
        }

        /// <summary>Authenticates the user and returns a JWT on success.</summary>
        public async Task<ServiceResult<string>> LoginAsync(string email, string password)
        {
            var user = await _userRepository.GetByEmailAsync(email);
            if (user is null)
                return ServiceResult<string>.Fail(ErrorCodes.Unauthorized, "Invalid email or password.");

            var authData = await _authDataRepository.GetByUserIdAsync(user.Id);
            if (authData is null)
                return ServiceResult<string>.Fail(ErrorCodes.Unauthorized, "Invalid email or password.");

            if (!authData.VerifyPassword(password, _passwordHasher))
                return ServiceResult<string>.Fail(ErrorCodes.Unauthorized, "Invalid email or password.");

            var token = _jwtService.GenerateToken(user);

            // Store a refresh token so the client can request a new JWT later
            var refreshToken = Guid.NewGuid().ToString("N");
            authData.SetRefreshToken(refreshToken, DateTime.UtcNow.AddDays(7));
            await _authDataRepository.UpdateAsync(authData);

            return ServiceResult<string>.Ok(token);
        }

        /// <summary>Issues a new JWT from a valid refresh token.</summary>
        public async Task<ServiceResult<string>> RefreshTokenAsync(string refreshToken)
        {
            // Refresh token lookup requires scanning — acceptable at current scale
            // Future: add a dedicated index or token collection if volume demands it
            var allUsers = await _userRepository.GetByEmailAsync(string.Empty);
            // Not feasible to scan all users by email; look up by refresh token via auth data
            // For now, this is a placeholder — the real implementation needs
            // IAuthDataRepository.GetByRefreshTokenAsync which can be added in a future step

            return ServiceResult<string>.Fail(ErrorCodes.NotFound, "Refresh token not found.");
        }

        /// <summary>Invalidates the refresh token for the given user.</summary>
        public async Task<bool> LogoutAsync(string userId)
        {
            var authData = await _authDataRepository.GetByUserIdAsync(userId);
            if (authData is null)
                return false;

            authData.ClearRefreshToken();
            await _authDataRepository.UpdateAsync(authData);
            return true;
        }
    }
}

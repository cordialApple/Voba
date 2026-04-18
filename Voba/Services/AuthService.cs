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

        /// <summary>Authenticates the user and returns an access token and refresh token on success.</summary>
        public async Task<ServiceResult<AuthTokens>> LoginAsync(string email, string password)
        {
            var user = await _userRepository.GetByEmailAsync(email);
            if (user is null)
                return ServiceResult<AuthTokens>.Fail(ErrorCodes.Unauthorized, "Invalid email or password.");

            var authData = await _authDataRepository.GetByUserIdAsync(user.Id);
            if (authData is null)
                return ServiceResult<AuthTokens>.Fail(ErrorCodes.Unauthorized, "Invalid email or password.");

            if (!authData.VerifyPassword(password, _passwordHasher))
                return ServiceResult<AuthTokens>.Fail(ErrorCodes.Unauthorized, "Invalid email or password.");

            var accessToken = _jwtService.GenerateToken(user);
            var refreshToken = Guid.NewGuid().ToString("N");
            authData.SetRefreshToken(refreshToken, DateTime.UtcNow.AddDays(7));
            await _authDataRepository.UpdateAsync(authData);

            return ServiceResult<AuthTokens>.Ok(new AuthTokens(accessToken, refreshToken));
        }

        /// <summary>Issues a new token pair from a valid refresh token.</summary>
        public async Task<ServiceResult<AuthTokens>> RefreshTokenAsync(string refreshToken)
        {
            var authData = await _authDataRepository.GetByRefreshTokenAsync(refreshToken);
            if (authData is null || authData.TokenExpiry < DateTime.UtcNow)
                return ServiceResult<AuthTokens>.Fail(ErrorCodes.Unauthorized, "Invalid or expired refresh token.");

            var user = await _userRepository.GetByIdAsync(authData.UserId);
            if (user is null)
                return ServiceResult<AuthTokens>.Fail(ErrorCodes.NotFound, "User not found.");

            var accessToken = _jwtService.GenerateToken(user);
            var newRefreshToken = Guid.NewGuid().ToString("N");
            authData.SetRefreshToken(newRefreshToken, DateTime.UtcNow.AddDays(7));
            await _authDataRepository.UpdateAsync(authData);

            return ServiceResult<AuthTokens>.Ok(new AuthTokens(accessToken, newRefreshToken));
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

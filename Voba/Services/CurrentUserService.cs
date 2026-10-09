namespace Voba.Services
{
    // Holds the signed-in user for the lifetime of the app process.
    // Registered as a singleton so pages share one session.
    public interface ICurrentUserService
    {
        string? UserId { get; }
        string? Email { get; }
        bool IsAuthenticated { get; }
        void SetUser(string userId, string email);
        void Clear();
    }

    public class CurrentUserService : ICurrentUserService
    {
        public string? UserId { get; private set; }
        public string? Email { get; private set; }
        public bool IsAuthenticated => !string.IsNullOrEmpty(UserId);

        public void SetUser(string userId, string email)
        {
            UserId = userId;
            Email = email;
        }

        public void Clear()
        {
            UserId = null;
            Email = null;
        }
    }
}

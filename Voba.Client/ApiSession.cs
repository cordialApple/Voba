namespace Voba.Client;

public sealed class ApiSession
{
    private readonly object _gate = new();
    private SessionState? _state;
    private long _nextEpoch;

    public bool IsAuthenticated => Snapshot() is not null;
    public string? UserId => Snapshot()?.UserId;
    public string? Email => Snapshot()?.Email;

    public SessionState? Snapshot()
    {
        lock (_gate)
            return _state;
    }

    public void Set(string userId, string email, string accessToken, string refreshToken)
    {
        lock (_gate)
            _state = new SessionState(userId, email, accessToken, refreshToken)
            {
                Epoch = ++_nextEpoch
            };
    }

    public SessionState? TryRotate(SessionState expected, string accessToken,
        string refreshToken)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(_state, expected))
                return null;
            _state = expected with { AccessToken = accessToken, RefreshToken = refreshToken };
            return _state;
        }
    }

    public void ClearIfCurrent(SessionState expected)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_state, expected))
                _state = null;
        }
    }

    public void ClearIfEpoch(long epoch)
    {
        lock (_gate)
        {
            if (_state?.Epoch == epoch)
                _state = null;
        }
    }

    public void Clear()
    {
        lock (_gate)
            _state = null;
    }
}

public sealed record SessionState(string UserId, string Email,
    string AccessToken, string RefreshToken)
{
    public long Epoch { get; init; }
}

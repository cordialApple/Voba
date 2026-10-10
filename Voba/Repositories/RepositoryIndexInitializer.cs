namespace Voba.Repositories;

public sealed class RepositoryIndexInitializer
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile bool _ready;

    public async Task EnsureAsync(Func<Task> initialize)
    {
        if (_ready)
            return;

        await _gate.WaitAsync();
        try
        {
            if (_ready)
                return;
            await initialize();
            _ready = true;
        }
        finally
        {
            _gate.Release();
        }
    }
}

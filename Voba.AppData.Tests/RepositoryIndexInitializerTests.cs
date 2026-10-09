using Voba.Repositories;
using Xunit;

namespace Voba.AppData.Tests;

public class RepositoryIndexInitializerTests
{
    [Fact]
    public async Task Failure_allows_later_retry()
    {
        var initializer = new RepositoryIndexInitializer();
        var calls = 0;
        Task Initialize()
        {
            calls++;
            return calls == 1
                ? Task.FromException(new InvalidOperationException("offline"))
                : Task.CompletedTask;
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => initializer.EnsureAsync(Initialize));
        await initializer.EnsureAsync(Initialize);
        await initializer.EnsureAsync(Initialize);

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Concurrent_calls_initialize_once()
    {
        var initializer = new RepositoryIndexInitializer();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        async Task Initialize()
        {
            Interlocked.Increment(ref calls);
            await release.Task;
        }

        var first = initializer.EnsureAsync(Initialize);
        var second = initializer.EnsureAsync(Initialize);
        release.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(1, calls);
    }
}

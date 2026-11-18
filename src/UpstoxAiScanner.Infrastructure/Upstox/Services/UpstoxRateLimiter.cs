using System.Threading.Channels;

namespace UpstoxAiScanner.Infrastructure.Upstox.Services;

/// <summary>
/// Token bucket rate limiter to throttle requests and prevent hitting Upstox API limits.
/// </summary>
public class UpstoxRateLimiter : IDisposable
{
    private readonly SemaphoreSlim _semaphore;
    private readonly int _maxRequestsPerSecond;
    private readonly PeriodicTimer _timer;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    public UpstoxRateLimiter(int maxRequestsPerSecond = 10)
    {
        _maxRequestsPerSecond = Math.Max(1, maxRequestsPerSecond);
        _semaphore = new SemaphoreSlim(_maxRequestsPerSecond, _maxRequestsPerSecond);
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        _ = ReplenishTokensLoopAsync(_cts.Token);
    }

    public async Task WaitForSlotAsync(CancellationToken cancellationToken = default)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
        await _semaphore.WaitAsync(linkedCts.Token);
    }

    private async Task ReplenishTokensLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (await _timer.WaitForNextTickAsync(cancellationToken))
            {
                var releaseCount = _maxRequestsPerSecond - _semaphore.CurrentCount;
                if (releaseCount > 0)
                {
                    _semaphore.Release(releaseCount);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when disposing
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
        _timer.Dispose();
        _semaphore.Dispose();
    }
}

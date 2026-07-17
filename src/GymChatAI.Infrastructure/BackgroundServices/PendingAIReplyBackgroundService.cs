using GymChatAI.Application.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GymChatAI.Infrastructure.BackgroundServices;

/// <summary>
/// Runs RetryPendingAIRepliesHandler on a short, fixed interval. Unlike the loyalty engine
/// (daily campaigns), these are customer-facing replies that failed once - a few minutes
/// between retries is appropriate, since the underlying cause (rate limiting, a transient
/// 503) usually clears up within that window.
/// </summary>
public class PendingAIReplyBackgroundService : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(20);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PendingAIReplyBackgroundService> _logger;

    public PendingAIReplyBackgroundService(IServiceScopeFactory scopeFactory, ILogger<PendingAIReplyBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<RetryPendingAIRepliesHandler>();
                await handler.ProcessPendingRepliesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pending AI reply retry pass failed unexpectedly.");
            }

            try
            {
                await Task.Delay(RunInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }
}

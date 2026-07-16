using GymChatAI.Application.Abstractions;
using GymChatAI.Application.Loyalty;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GymChatAI.Infrastructure.BackgroundServices;

/// <summary>
/// Runs LoyaltyEngineHandler.ProcessAutomaticCampaignsForGymAsync for every active gym on a
/// fixed interval. Every check the handler performs is idempotent (see
/// ICampaignMessageRepository), so running this more than once a day - e.g. after a restart -
/// never results in duplicate messages.
/// </summary>
public class LoyaltyEngineBackgroundService : BackgroundService
{
    // 24h in production; kept short enough to observe during a demo without waiting a full day.
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LoyaltyEngineBackgroundService> _logger;

    public LoyaltyEngineBackgroundService(IServiceScopeFactory scopeFactory, ILogger<LoyaltyEngineBackgroundService> logger)
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
            await RunOnceAsync(stoppingToken);

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

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var gymRepository = scope.ServiceProvider.GetRequiredService<IGymRepository>();
        var loyaltyEngine = scope.ServiceProvider.GetRequiredService<LoyaltyEngineHandler>();

        var gyms = await gymRepository.GetAllActiveAsync(cancellationToken);
        _logger.LogInformation("Loyalty engine: evaluating campaigns for {GymCount} gym(s).", gyms.Count);

        foreach (var gym in gyms)
        {
            try
            {
                await loyaltyEngine.ProcessAutomaticCampaignsForGymAsync(gym.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Loyalty engine failed processing gym {GymId}.", gym.Id);
            }
        }
    }
}

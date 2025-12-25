using GameSwap.Functions.Models.Notifications;

namespace GameSwap.Functions.Notifications;

public sealed class NoOpNotificationService : INotificationService
{
    public Task EnqueueAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

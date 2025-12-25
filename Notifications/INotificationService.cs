using GameSwap.Functions.Models.Notifications;

namespace GameSwap.Functions.Notifications;

public interface INotificationService
{
    Task EnqueueAsync(NotificationRequest request, CancellationToken cancellationToken = default);
}

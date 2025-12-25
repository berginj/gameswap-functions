using GameSwap.Functions.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        var tableServiceClient = GameSwap.Functions.Storage.TableClients.CreateServiceClient(context.Configuration);
        services.AddSingleton(tableServiceClient);
        services.AddHostedService<GameSwap.Functions.Storage.TableStartup>();
        services.AddSingleton<INotificationService, NoOpNotificationService>();
    })
    .Build();

host.Run();

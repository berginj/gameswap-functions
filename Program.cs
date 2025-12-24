using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        var tableServiceClient = GameSwap.Functions.Storage.TableClients.CreateServiceClient(context.Configuration);
        services.AddSingleton(tableServiceClient);
        services.AddHostedService<GameSwap.Functions.Storage.TableStartup>();
    })
    .Build();

host.Run();

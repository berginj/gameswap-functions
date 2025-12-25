using GameSwap.Functions.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(worker =>
    {
        worker.UseMiddleware<JwtValidationMiddleware>();
    })
    .ConfigureServices((context, services) =>
    {
        var tableServiceClient = GameSwap.Functions.Storage.TableClients.CreateServiceClient(context.Configuration);
        services.AddSingleton(tableServiceClient);
        services.AddHostedService<GameSwap.Functions.Storage.TableStartup>();
    })
    .Build();

host.Run();

using Azure.Data.Tables;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        var storageConn = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        services.AddSingleton(new TableServiceClient(storageConn));
    })
    .Build();

host.Run();

using System.Net;
using System.Text;
using Azure.Data.Tables;
using GameSwap.Functions.Functions;
using GameSwap.Functions.Storage;
using GameSwap.Functions.Tests.Infrastructure;
using Microsoft.Extensions.Logging;
using Xunit;

namespace GameSwap.Functions.Tests.Integration;

public class HttpTriggerSmokeTests
{
    [Fact]
    public async Task ImportFields_returns_bad_request_when_missing_league_header()
    {
        var context = new TestFunctionContext();
        var request = new TestHttpRequestData(context)
        {
            Method = "POST",
            Body = new MemoryStream(Encoding.UTF8.GetBytes("fieldKey,parkName,fieldName\nP/F,Park,Field"))
        };

        using var loggerFactory = LoggerFactory.Create(builder => { });
        var function = new ImportFields(loggerFactory, new TableServiceClient("UseDevelopmentStorage=true"));

        var response = await function.Run(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateSlotRequest_requires_division_route_param()
    {
        var context = new TestFunctionContext();
        var request = new TestHttpRequestData(context) { Method = "POST" };
        request.Headers.Add(Constants.LEAGUE_HEADER_NAME, "league-1");

        using var loggerFactory = LoggerFactory.Create(builder => { });
        var function = new CreateSlotRequest(loggerFactory, new TableServiceClient("UseDevelopmentStorage=true"));

        var response = await function.Run(request, "", "slot-1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

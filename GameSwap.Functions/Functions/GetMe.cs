using System.Net;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using GameSwap.Functions.Storage;

namespace GameSwap.Functions.Functions;

public class GetMe
{
    private readonly ILogger _log;
    private readonly TableServiceClient _svc;

    public GetMe(ILoggerFactory lf, TableServiceClient tableServiceClient)
    {
        _log = lf.CreateLogger<GetMe>();
        _svc = tableServiceClient;
    }

    [Function("GetMe")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me")] HttpRequestData req)
    {
        try
        {
            var me = IdentityUtil.GetMe(req);

            // leagueId is optional for this endpoint; if provided we add membership context
            var leagueId = "";
            try { leagueId = ApiGuards.RequireLeagueId(req); } catch { /* ignore */ }

            var isMember = false;
            var role = "";

            if (!string.IsNullOrWhiteSpace(leagueId))
            {
                isMember = await ApiGuards.IsMemberAsync(_svc, me.UserId, leagueId);
                if (isMember)
                    role = await ApiGuards.GetRoleAsync(_svc, me.UserId, leagueId);
            }

            return HttpUtil.Json(req, HttpStatusCode.OK, new
            {
                userId = me.UserId,
                email = me.Email,
                leagueId,
                isMember,
                role
            });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "GetMe failed");
            return HttpUtil.Text(req, HttpStatusCode.InternalServerError, "Internal Server Error");
        }
    }
}

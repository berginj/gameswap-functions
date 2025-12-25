using System.Net;
using Azure;
using Azure.Data.Tables;
using GameSwap.Functions.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace GameSwap.Functions.Functions;

public class DeleteSlot
{
    private readonly ILogger _log;
    private readonly TableServiceClient _svc;

    private const string SlotsTableName = Constants.Tables.Slots;

    public DeleteSlot(ILoggerFactory lf, TableServiceClient tableServiceClient)
    {
        _log = lf.CreateLogger<DeleteSlot>();
        _svc = tableServiceClient;
    }

    [Function("DeleteSlot")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "slots/{division}/{slotId}")] HttpRequestData req,
        string division,
        string slotId)
    {
        try
        {
            var leagueId = ApiGuards.RequireLeagueId(req);
            var me = IdentityUtil.GetMe(req);
            await ApiGuards.RequireLeagueAdminAsync(_svc, me.UserId, leagueId);

            division = (division ?? "").Trim();
            slotId = (slotId ?? "").Trim();
            if (string.IsNullOrWhiteSpace(division) || string.IsNullOrWhiteSpace(slotId))
                return ApiResponses.Error(req, HttpStatusCode.BadRequest, "BAD_REQUEST", "division and slotId are required");

            var table = await TableClients.GetTableAsync(_svc, SlotsTableName);
            var pk = Constants.Pk.Slots(leagueId, division);

            try
            {
                await table.DeleteEntityAsync(pk, slotId);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return ApiResponses.Error(req, HttpStatusCode.NotFound, "NOT_FOUND", "Slot not found");
            }

            return ApiResponses.Ok(req, new { ok = true, slotId, division });
        }
        catch (ApiGuards.HttpError ex) { return ApiResponses.FromHttpError(req, ex); }
        catch (Exception ex)
        {
            _log.LogError(ex, "DeleteSlot failed");
            return ApiResponses.Error(req, HttpStatusCode.InternalServerError, "INTERNAL", "Internal Server Error");
        }
    }
}

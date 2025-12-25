using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using WorkerHttpRequestData = Microsoft.Azure.Functions.Worker.Http.HttpRequestData;

namespace GameSwap.Functions.Auth;

public sealed class JwtValidationMiddleware : IFunctionsWorkerMiddleware
{
    private readonly AuthOptions _options;
    private readonly ILogger<JwtValidationMiddleware> _log;
    private readonly ConfigurationManager<OpenIdConnectConfiguration>? _configurationManager;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    public JwtValidationMiddleware(IConfiguration configuration, ILogger<JwtValidationMiddleware> log)
    {
        _options = configuration.GetSection("Auth").Get<AuthOptions>() ?? new AuthOptions();
        _log = log;

        var authority = _options.ResolveAuthority();
        if (!string.IsNullOrWhiteSpace(authority))
        {
            var metadata = $"{authority}/v2.0/.well-known/openid-configuration";
            _configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                metadata,
                new OpenIdConnectConfigurationRetriever());
        }
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var request = await context.GetHttpRequestDataAsync();
        if (request is null)
        {
            await next(context);
            return;
        }

        if (HasSwaPrincipal(request))
        {
            await next(context);
            return;
        }

        if (!TryGetBearerToken(request, out var token))
        {
            if (_options.RequireAuthentication)
            {
                await SetUnauthorizedAsync(context, request, "Missing bearer token.");
                return;
            }

            await next(context);
            return;
        }

        try
        {
            var validationParameters = await BuildValidationParametersAsync();
            var principal = _tokenHandler.ValidateToken(token, validationParameters, out _);
            ApplyClaimsToHeaders(request, principal.Claims);
            await next(context);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "JWT validation failed");
            await SetUnauthorizedAsync(context, request, "Invalid bearer token.");
        }
    }

    private static bool HasSwaPrincipal(WorkerHttpRequestData request)
        => request.Headers.TryGetValues("x-ms-client-principal", out _)
            || request.Headers.TryGetValues("X-MS-CLIENT-PRINCIPAL", out _);

    private static bool TryGetBearerToken(WorkerHttpRequestData request, out string token)
    {
        token = string.Empty;
        if (!request.Headers.TryGetValues("Authorization", out var values)) return false;
        var raw = values.FirstOrDefault() ?? string.Empty;
        if (!raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return false;
        token = raw["Bearer ".Length..].Trim();
        return !string.IsNullOrWhiteSpace(token);
    }

    private async Task<TokenValidationParameters> BuildValidationParametersAsync()
    {
        var audiences = _options.ResolveAudiences().ToArray();
        var issuer = _options.ResolveIssuer();

        var parameters = new TokenValidationParameters
        {
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ValidateIssuer = !string.IsNullOrWhiteSpace(issuer),
            ValidIssuers = issuer is null ? null : new[] { issuer },
            ValidateAudience = audiences.Length > 0,
            ValidAudiences = audiences.Length > 0 ? audiences : null,
            ClockSkew = TimeSpan.FromMinutes(2),
        };

        if (_configurationManager is not null)
        {
            var config = await _configurationManager.GetConfigurationAsync(CancellationToken.None);
            parameters.IssuerSigningKeys = config.SigningKeys;
        }

        return parameters;
    }

    private static void ApplyClaimsToHeaders(WorkerHttpRequestData request, IEnumerable<Claim> claims)
    {
        var userId = claims.FirstOrDefault(c =>
                string.Equals(c.Type, "oid", StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.Type, "sub", StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.Type, ClaimTypes.NameIdentifier, StringComparison.OrdinalIgnoreCase))
            ?.Value;

        var email = claims.FirstOrDefault(c =>
                string.Equals(c.Type, "preferred_username", StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.Type, "email", StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.Type, "upn", StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.Type, ClaimTypes.Email, StringComparison.OrdinalIgnoreCase))
            ?.Value;

        var roles = claims.Where(c =>
                string.Equals(c.Type, "roles", StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.Type, "role", StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (!string.IsNullOrWhiteSpace(userId))
        {
            request.Headers.Remove("x-user-id");
            request.Headers.Add("x-user-id", userId);
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            request.Headers.Remove("x-user-email");
            request.Headers.Add("x-user-email", email);
        }

        if (roles.Length > 0)
        {
            request.Headers.Remove("x-user-roles");
            request.Headers.Add("x-user-roles", string.Join(',', roles));
        }
    }

    private static async Task SetUnauthorizedAsync(FunctionContext context, WorkerHttpRequestData request, string message)
    {
        var response = request.CreateResponse(HttpStatusCode.Unauthorized);
        await response.WriteStringAsync(message);
        context.GetInvocationResult().Value = response;
    }
}

# Authentication & Authorization (Entra ID + Azure Functions)

## UI (Vite + React)

The UI uses MSAL to sign in with Microsoft Entra ID and request an access token for the API.

**Required environment variables (fsa-www):**

| Variable | Description |
| --- | --- |
| `VITE_AAD_TENANT_ID` | Tenant ID for your Entra directory. |
| `VITE_AAD_CLIENT_ID` | Client ID for the UI app registration. |
| `VITE_AAD_API_SCOPE` | API scope to request (example: `api://<api-app-id>/access_as_user`). |
| `VITE_API_BASE_URL` | Base URL for API calls (default: `/api`). |

**App registration (UI):**

1. Create a *Single-page application* registration in Entra.
2. Add redirect URIs for your local/dev/prod domains (for example, `http://localhost:5173`).
3. Grant API permissions to the backend scope (see API registration below).

## API (Azure Functions)

The Functions pipeline validates bearer tokens and maps key claims to headers used by the existing identity utilities.

**Required environment variables (Functions):**

| Variable | Description |
| --- | --- |
| `Auth__TenantId` | Tenant ID for Entra (used to resolve authority). |
| `Auth__ClientId` | Client ID (used when `Auth__Audience` is not set). |
| `Auth__Audience` | API App ID URI or client ID to validate the `aud` claim. |
| `Auth__Authority` | Optional explicit authority (overrides tenant). |
| `Auth__Issuer` | Optional explicit issuer (overrides authority). |
| `Auth__RequireAuthentication` | `true` to reject requests without a bearer token. |
| `AUTH_ADMIN_ROLES` | Comma-separated list of Entra app roles treated as global admin (default: `GlobalAdmin,Admin`). |

**App registration (API):**

1. Create an *API* app registration.
2. Expose an API scope (for example `access_as_user`).
3. Add app roles for admin actions (for example `GlobalAdmin`).
4. Assign roles to users or groups.

**How admin authorization works:**

* Tokens with a role in `AUTH_ADMIN_ROLES` are treated as global admins.
* If no admin role is present, the `GameSwapGlobalAdmins` table is used as the fallback.


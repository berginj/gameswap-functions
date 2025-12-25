const tenantId = import.meta.env.VITE_AAD_TENANT_ID
const clientId = import.meta.env.VITE_AAD_CLIENT_ID
const apiScope = import.meta.env.VITE_AAD_API_SCOPE

export const msalConfig = {
  auth: {
    clientId,
    authority: tenantId
      ? `https://login.microsoftonline.com/${tenantId}`
      : undefined,
    redirectUri: window.location.origin,
  },
  cache: {
    cacheLocation: 'sessionStorage',
    storeAuthStateInCookie: false,
  },
}

const baseScopes = ['openid', 'profile', 'email']

export const loginRequest = {
  scopes: apiScope ? [...baseScopes, apiScope] : baseScopes,
}

export const apiConfig = {
  baseUrl: import.meta.env.VITE_API_BASE_URL || '/api',
  apiScope,
}

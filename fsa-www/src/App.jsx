import { useMemo, useState } from 'react'
import { useIsAuthenticated, useMsal } from '@azure/msal-react'
import './App.css'
import { apiConfig, loginRequest } from './authConfig'

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? ''

async function apiFetch(path, { leagueId, body, method = 'GET' } = {}) {
  const headers = {}
  if (leagueId) {
    headers['x-league-id'] = leagueId
  }
  if (body) {
    headers['Content-Type'] = 'application/json'
  }

  const response = await fetch(`${API_BASE}${path}`, {
    method,
    headers,
    body: body ? JSON.stringify(body) : undefined,
  })

  const text = await response.text()
  let data = null
  if (text) {
    try {
      data = JSON.parse(text)
    } catch (err) {
      data = { message: text }
    }
  }

  if (!response.ok) {
    const message = data?.message || data?.error || response.statusText
    throw new Error(message)
  }

  return data
}

function App() {
  const { instance, accounts } = useMsal()
  const isAuthenticated = useIsAuthenticated()
  const [apiResult, setApiResult] = useState(null)
  const [apiError, setApiError] = useState('')

  const account = useMemo(() => accounts[0], [accounts])

  const handleLogin = async () => {
    setApiError('')
    await instance.loginPopup(loginRequest)
  }

  const handleLogout = async () => {
    setApiError('')
    setApiResult(null)
    if (account) {
      await instance.logoutPopup({ account })
    } else {
      await instance.logoutPopup()
    }
  }

  const handleFetchProfile = async () => {
    setApiError('')
    setApiResult(null)

    try {
      const tokenResponse = await instance.acquireTokenSilent({
        ...loginRequest,
        account,
      })

      const response = await fetch(`${apiConfig.baseUrl}/me`, {
        headers: {
          Authorization: `Bearer ${tokenResponse.accessToken}`,
        },
      })

      if (!response.ok) {
        const text = await response.text()
        throw new Error(text || `Request failed (${response.status})`)
      }

      const data = await response.json()
      setApiResult(data)
    } catch (error) {
      setApiError(error?.message || 'Unable to fetch profile.')
    }
  }

function App() {
  return (
    <>
      <div className="card">
        <h1>GameSwap Admin</h1>
        <p className="subtitle">
          Sign in with Microsoft Entra ID to manage leagues and schedules.
        </p>
        <div className="actions">
          {!isAuthenticated ? (
            <button onClick={handleLogin}>Sign in</button>
          ) : (
            <>
              <button onClick={handleFetchProfile}>Load profile</button>
              <button className="secondary" onClick={handleLogout}>
                Sign out
              </button>
            </>
          )}
        </div>
        <div className="status">
          <div>
            <strong>Status:</strong>{' '}
            {isAuthenticated ? 'Authenticated' : 'Signed out'}
          </div>
          {account?.username && (
            <div>
              <strong>Account:</strong> {account.username}
            </div>
          )}
        </div>
        {apiError && <pre className="error">{apiError}</pre>}
        {apiResult && (
          <pre className="result">{JSON.stringify(apiResult, null, 2)}</pre>
        )}
      </div>
    </>
  )
}

export default App

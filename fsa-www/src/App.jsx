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

const IMPORT_TYPES = [
  {
    key: 'fields',
    label: 'Fields',
    csvHeaders: [
      'fieldKey',
      'parkName',
      'fieldName',
      'displayName',
      'address',
      'notes',
      'status',
      'isActive',
      'lights',
      'battingCage',
      'portableMound',
      'fieldLockCode',
      'fieldNotes'
    ]
  },
  {
    key: 'teams',
    label: 'Teams',
    csvHeaders: [
      'division',
      'teamId',
      'name',
      'primaryContactName',
      'primaryContactEmail',
      'primaryContactPhone'
    ]
  },
  {
    key: 'events',
    label: 'Events',
    csvHeaders: [
      'type',
      'division',
      'teamId',
      'title',
      'eventDate',
      'startTime',
      'endTime',
      'location',
      'notes',
      'status'
    ]
  }
]

const parseCsv = (text) => {
  const rows = []
  let row = []
  let field = ''
  let inQuotes = false

  for (let i = 0; i < text.length; i += 1) {
    const c = text[i]
    if (inQuotes) {
      if (c === '"') {
        if (text[i + 1] === '"') {
          field += '"'
          i += 1
        } else {
          inQuotes = false
        }
      } else {
        field += c
      }
    } else if (c === '"') {
      inQuotes = true
    } else if (c === ',') {
      row.push(field)
      field = ''
    } else if (c === '\n') {
      row.push(field)
      rows.push(row)
      row = []
      field = ''
    } else if (c !== '\r') {
      field += c
    }
  }

  row.push(field)
  rows.push(row)
  return rows
}

const parseGridText = (text) => {
  const lines = text.replace(/\r\n/g, '\n').replace(/\r/g, '\n').split('\n')
  return lines.filter((line) => line.trim().length > 0).map((line) => line.split('\t'))
}

const downloadCsvTemplate = (headers, filename) => {
  const csv = `${headers.join(',')}\n`
  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = filename
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(url)
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

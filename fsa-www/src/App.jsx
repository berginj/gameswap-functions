import { useState } from 'react'
import './App.css'

function App() {
  const [loginEmail, setLoginEmail] = useState('')
  const [loggedInEmail, setLoggedInEmail] = useState('')
  const [eventName, setEventName] = useState('')
  const [eventDate, setEventDate] = useState('')
  const [eventStatus, setEventStatus] = useState('')
  const [importFileName, setImportFileName] = useState('')

  const handleLogin = (event) => {
    event.preventDefault()
    if (loginEmail.trim()) {
      setLoggedInEmail(loginEmail.trim())
    }
  }

  const handleCreateEvent = (event) => {
    event.preventDefault()
    if (eventName.trim() && eventDate.trim()) {
      setEventStatus(`Event "${eventName.trim()}" scheduled for ${eventDate.trim()}`)
    }
  }

  const handleImport = (event) => {
    const file = event.target.files?.[0]
    setImportFileName(file ? file.name : '')
  }

  return (
    <div className="app">
      <header>
        <h1>GameSwap Scheduler</h1>
        <p>Manage logins, events, and bulk imports in one place.</p>
      </header>

      <section aria-labelledby="login-title" className="card">
        <h2 id="login-title">Login</h2>
        <form onSubmit={handleLogin} className="form">
          <label htmlFor="login-email">Email</label>
          <input
            id="login-email"
            data-testid="login-email"
            type="email"
            value={loginEmail}
            onChange={(event) => setLoginEmail(event.target.value)}
            placeholder="coach@example.com"
          />
          <button data-testid="login-submit" type="submit">
            Sign in
          </button>
        </form>
        {loggedInEmail && (
          <p data-testid="login-status" className="status">
            Logged in as {loggedInEmail}
          </p>
        )}
      </section>

      <section aria-labelledby="event-title" className="card">
        <h2 id="event-title">Create Event</h2>
        <form onSubmit={handleCreateEvent} className="form">
          <label htmlFor="event-name">Event name</label>
          <input
            id="event-name"
            data-testid="event-name"
            value={eventName}
            onChange={(event) => setEventName(event.target.value)}
            placeholder="Field cleanup"
          />
          <label htmlFor="event-date">Date</label>
          <input
            id="event-date"
            data-testid="event-date"
            type="date"
            value={eventDate}
            onChange={(event) => setEventDate(event.target.value)}
          />
          <button data-testid="event-submit" type="submit">
            Create event
          </button>
        </form>
        {eventStatus && (
          <p data-testid="event-status" className="status">
            {eventStatus}
          </p>
        )}
      </section>

      <section aria-labelledby="import-title" className="card">
        <h2 id="import-title">Bulk Import</h2>
        <label htmlFor="import-file">Upload CSV</label>
        <input
          id="import-file"
          data-testid="import-file"
          type="file"
          accept=".csv"
          onChange={handleImport}
        />
        {importFileName && (
          <p data-testid="import-status" className="status">
            Ready to import: {importFileName}
          </p>
        )}
      </section>
    </div>
  )
}

export default App

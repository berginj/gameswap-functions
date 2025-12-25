import { useEffect, useMemo, useState } from 'react'
import './App.css'

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
  const [leagueId, setLeagueId] = useState('')
  const [status, setStatus] = useState('')
  const [me, setMe] = useState(null)
  const [leagues, setLeagues] = useState([])
  const [leagueDetail, setLeagueDetail] = useState(null)
  const [divisions, setDivisions] = useState([])
  const [divisionForm, setDivisionForm] = useState({
    code: '',
    name: '',
    isActive: true,
  })
  const [selectedDivision, setSelectedDivision] = useState(null)
  const [teams, setTeams] = useState([])
  const [teamForm, setTeamForm] = useState({
    division: '',
    teamId: '',
    name: '',
    primaryContact: { name: '', email: '', phone: '' },
  })
  const [selectedTeam, setSelectedTeam] = useState(null)
  const [newLeagueForm, setNewLeagueForm] = useState({
    leagueId: '',
    name: '',
    timezone: 'America/New_York',
  })

  const leagueRole = useMemo(() => {
    if (!leagueId || !me?.memberships) return null
    return me.memberships.find((m) => m.leagueId === leagueId)?.role ?? null
  }, [leagueId, me])

  const canEditLeague = Boolean(
    me?.isGlobalAdmin || leagueRole === 'LeagueAdmin'
  )

  useEffect(() => {
    const loadMe = async () => {
      try {
        const data = await apiFetch('/me')
        setMe(data)
      } catch (err) {
        setStatus(`Sign in required: ${err.message}`)
      }
    }

    const loadLeagues = async () => {
      try {
        const data = await apiFetch('/leagues')
        setLeagues(data)
      } catch (err) {
        setStatus(`Failed to load leagues: ${err.message}`)
      }
    }

    loadMe()
    loadLeagues()
  }, [])

  const loadLeagueData = async () => {
    if (!leagueId.trim()) {
      setStatus('Enter a league id to load.')
      return
    }

    setStatus('Loading league data...')

    try {
      const [leagueData, divisionData, teamData] = await Promise.all([
        apiFetch('/league', { leagueId }),
        apiFetch('/divisions', { leagueId }),
        apiFetch('/teams', { leagueId }),
      ])
      setLeagueDetail(leagueData)
      setDivisions(divisionData)
      setTeams(teamData)
      setStatus('League data loaded.')
    } catch (err) {
      setStatus(`Failed to load league data: ${err.message}`)
    }
  }

  const handleLeagueSave = async () => {
    if (!leagueId.trim()) return
    try {
      setStatus('Saving league...')
      await apiFetch('/league', {
        leagueId,
        method: 'PATCH',
        body: leagueDetail,
      })
      setStatus('League updated.')
    } catch (err) {
      setStatus(`Failed to update league: ${err.message}`)
    }
  }

  const handleNewLeague = async (event) => {
    event.preventDefault()
    try {
      setStatus('Creating league...')
      await apiFetch('/admin/leagues', {
        method: 'POST',
        body: newLeagueForm,
      })
      setNewLeagueForm({
        leagueId: '',
        name: '',
        timezone: 'America/New_York',
      })
      const data = await apiFetch('/leagues')
      setLeagues(data)
      setStatus('League created.')
    } catch (err) {
      setStatus(`Failed to create league: ${err.message}`)
    }
  }

  const handleDivisionCreate = async (event) => {
    event.preventDefault()
    try {
      setStatus('Creating division...')
      await apiFetch('/divisions', {
        leagueId,
        method: 'POST',
        body: divisionForm,
      })
      const data = await apiFetch('/divisions', { leagueId })
      setDivisions(data)
      setDivisionForm({ code: '', name: '', isActive: true })
      setStatus('Division created.')
    } catch (err) {
      setStatus(`Failed to create division: ${err.message}`)
    }
  }

  const handleDivisionSelect = (division) => {
    setSelectedDivision(division)
  }

  const handleDivisionSave = async () => {
    if (!selectedDivision) return
    try {
      setStatus('Saving division...')
      await apiFetch(`/divisions/${selectedDivision.code}`, {
        leagueId,
        method: 'PATCH',
        body: {
          name: selectedDivision.name,
          isActive: selectedDivision.isActive,
        },
      })
      const data = await apiFetch('/divisions', { leagueId })
      setDivisions(data)
      setStatus('Division updated.')
    } catch (err) {
      setStatus(`Failed to update division: ${err.message}`)
    }
  }

  const handleTeamCreate = async (event) => {
    event.preventDefault()
    try {
      setStatus('Creating team...')
      await apiFetch('/teams', {
        leagueId,
        method: 'POST',
        body: teamForm,
      })
      const data = await apiFetch('/teams', { leagueId })
      setTeams(data)
      setTeamForm({
        division: '',
        teamId: '',
        name: '',
        primaryContact: { name: '', email: '', phone: '' },
      })
      setStatus('Team created.')
    } catch (err) {
      setStatus(`Failed to create team: ${err.message}`)
    }
  }

  const handleTeamSelect = (team) => {
    setSelectedTeam(team)
  }

  const handleTeamSave = async () => {
    if (!selectedTeam) return
    try {
      setStatus('Saving team...')
      await apiFetch(`/teams/${selectedTeam.division}/${selectedTeam.teamId}`, {
        leagueId,
        method: 'PATCH',
        body: {
          name: selectedTeam.name,
          primaryContact: selectedTeam.primaryContact,
        },
      })
      const data = await apiFetch('/teams', { leagueId })
      setTeams(data)
      setStatus('Team updated.')
    } catch (err) {
      setStatus(`Failed to update team: ${err.message}`)
    }
  }

  const handleTeamDelete = async () => {
    if (!selectedTeam) return
    try {
      setStatus('Deleting team...')
      await apiFetch(`/teams/${selectedTeam.division}/${selectedTeam.teamId}`, {
        leagueId,
        method: 'DELETE',
      })
      const data = await apiFetch('/teams', { leagueId })
      setTeams(data)
      setSelectedTeam(null)
      setStatus('Team deleted.')
    } catch (err) {
      setStatus(`Failed to delete team: ${err.message}`)
    }
  }

  return (
    <div className="app">
      <header className="app-header">
        <div>
          <h1>League Operations Console</h1>
          <p>Manage leagues, divisions, and teams with role-aware controls.</p>
        </div>
        <div className="me-badge">
          <div>
            <strong>User:</strong> {me?.email || 'Unknown'}
          </div>
          <div>
            <strong>Global Admin:</strong>{' '}
            {me?.isGlobalAdmin ? 'Yes' : 'No'}
          </div>
        </div>
      </header>

      <section className="panel">
        <div className="panel-header">
          <h2>League Scope</h2>
          <div className="status">{status}</div>
        </div>
        <div className="grid two">
          <label className="field">
            <span>League ID</span>
            <input
              value={leagueId}
              onChange={(event) => setLeagueId(event.target.value)}
              placeholder="e.g. spring-softball"
            />
          </label>
          <div className="field action-row">
            <button type="button" onClick={loadLeagueData}>
              Load League Data
            </button>
            <div className="role-pill">
              Role: {leagueRole || 'Not a member'}
            </div>
          </div>
        </div>
      </section>

      <section className="panel">
        <div className="panel-header">
          <h2>Leagues</h2>
          <p>Available leagues in the system.</p>
        </div>
        <div className="grid two">
          <div>
            <ul className="list">
              {leagues.map((league) => (
                <li key={league.leagueId} className="list-item">
                  <div>
                    <strong>{league.name}</strong>
                    <div className="muted">{league.leagueId}</div>
                  </div>
                  <span className="tag">{league.status}</span>
                </li>
              ))}
            </ul>
          </div>
          <form className="form" onSubmit={handleNewLeague}>
            <h3>Create League (Global Admin)</h3>
            <label className="field">
              <span>League ID</span>
              <input
                value={newLeagueForm.leagueId}
                onChange={(event) =>
                  setNewLeagueForm({
                    ...newLeagueForm,
                    leagueId: event.target.value,
                  })
                }
                disabled={!me?.isGlobalAdmin}
              />
            </label>
            <label className="field">
              <span>Name</span>
              <input
                value={newLeagueForm.name}
                onChange={(event) =>
                  setNewLeagueForm({
                    ...newLeagueForm,
                    name: event.target.value,
                  })
                }
                disabled={!me?.isGlobalAdmin}
              />
            </label>
            <label className="field">
              <span>Timezone</span>
              <input
                value={newLeagueForm.timezone}
                onChange={(event) =>
                  setNewLeagueForm({
                    ...newLeagueForm,
                    timezone: event.target.value,
                  })
                }
                disabled={!me?.isGlobalAdmin}
              />
            </label>
            <button type="submit" disabled={!me?.isGlobalAdmin}>
              Create League
            </button>
            {!me?.isGlobalAdmin && (
              <p className="muted">Global admin role required.</p>
            )}
          </form>
        </div>
      </section>

      <section className="panel">
        <div className="panel-header">
          <h2>League Settings</h2>
          <p>Update league metadata for the selected scope.</p>
        </div>
        {leagueDetail ? (
          <div className="grid two">
            <div className="form">
              <label className="field">
                <span>Name</span>
                <input
                  value={leagueDetail.name || ''}
                  onChange={(event) =>
                    setLeagueDetail({ ...leagueDetail, name: event.target.value })
                  }
                  disabled={!canEditLeague}
                />
              </label>
              <label className="field">
                <span>Timezone</span>
                <input
                  value={leagueDetail.timezone || ''}
                  onChange={(event) =>
                    setLeagueDetail({
                      ...leagueDetail,
                      timezone: event.target.value,
                    })
                  }
                  disabled={!canEditLeague}
                />
              </label>
              <label className="field">
                <span>Status</span>
                <input
                  value={leagueDetail.status || ''}
                  onChange={(event) =>
                    setLeagueDetail({
                      ...leagueDetail,
                      status: event.target.value,
                    })
                  }
                  disabled={!canEditLeague}
                />
              </label>
              <button type="button" onClick={handleLeagueSave} disabled={!canEditLeague}>
                Save League
              </button>
              {!canEditLeague && (
                <p className="muted">LeagueAdmin or Global Admin required.</p>
              )}
            </div>
            <div className="form">
              <h3>Primary Contact</h3>
              <label className="field">
                <span>Name</span>
                <input
                  value={leagueDetail.contact?.name || ''}
                  onChange={(event) =>
                    setLeagueDetail({
                      ...leagueDetail,
                      contact: {
                        ...leagueDetail.contact,
                        name: event.target.value,
                      },
                    })
                  }
                  disabled={!canEditLeague}
                />
              </label>
              <label className="field">
                <span>Email</span>
                <input
                  value={leagueDetail.contact?.email || ''}
                  onChange={(event) =>
                    setLeagueDetail({
                      ...leagueDetail,
                      contact: {
                        ...leagueDetail.contact,
                        email: event.target.value,
                      },
                    })
                  }
                  disabled={!canEditLeague}
                />
              </label>
              <label className="field">
                <span>Phone</span>
                <input
                  value={leagueDetail.contact?.phone || ''}
                  onChange={(event) =>
                    setLeagueDetail({
                      ...leagueDetail,
                      contact: {
                        ...leagueDetail.contact,
                        phone: event.target.value,
                      },
                    })
                  }
                  disabled={!canEditLeague}
                />
              </label>
            </div>
          </div>
        ) : (
          <p className="muted">Load a league to view settings.</p>
        )}
      </section>

      <section className="panel">
        <div className="panel-header">
          <h2>Divisions</h2>
          <p>Manage division catalog for the selected league.</p>
        </div>
        <div className="grid three">
          <div>
            <ul className="list">
              {divisions.map((division) => (
                <li
                  key={division.code}
                  className={`list-item ${selectedDivision?.code === division.code ? 'selected' : ''}`}
                  onClick={() => handleDivisionSelect({ ...division })}
                >
                  <div>
                    <strong>{division.name}</strong>
                    <div className="muted">{division.code}</div>
                  </div>
                  <span className={`tag ${division.isActive ? 'active' : 'inactive'}`}>
                    {division.isActive ? 'Active' : 'Inactive'}
                  </span>
                </li>
              ))}
            </ul>
          </div>
          <form className="form" onSubmit={handleDivisionCreate}>
            <h3>Create Division</h3>
            <label className="field">
              <span>Code</span>
              <input
                value={divisionForm.code}
                onChange={(event) =>
                  setDivisionForm({ ...divisionForm, code: event.target.value })
                }
                disabled={!canEditLeague}
              />
            </label>
            <label className="field">
              <span>Name</span>
              <input
                value={divisionForm.name}
                onChange={(event) =>
                  setDivisionForm({ ...divisionForm, name: event.target.value })
                }
                disabled={!canEditLeague}
              />
            </label>
            <label className="field checkbox">
              <input
                type="checkbox"
                checked={divisionForm.isActive}
                onChange={(event) =>
                  setDivisionForm({
                    ...divisionForm,
                    isActive: event.target.checked,
                  })
                }
                disabled={!canEditLeague}
              />
              Active
            </label>
            <button type="submit" disabled={!canEditLeague}>
              Create Division
            </button>
          </form>
          <div className="form">
            <h3>Division Details</h3>
            {selectedDivision ? (
              <>
                <label className="field">
                  <span>Code</span>
                  <input value={selectedDivision.code} disabled />
                </label>
                <label className="field">
                  <span>Name</span>
                  <input
                    value={selectedDivision.name}
                    onChange={(event) =>
                      setSelectedDivision({
                        ...selectedDivision,
                        name: event.target.value,
                      })
                    }
                    disabled={!canEditLeague}
                  />
                </label>
                <label className="field checkbox">
                  <input
                    type="checkbox"
                    checked={selectedDivision.isActive}
                    onChange={(event) =>
                      setSelectedDivision({
                        ...selectedDivision,
                        isActive: event.target.checked,
                      })
                    }
                    disabled={!canEditLeague}
                  />
                  Active
                </label>
                <button
                  type="button"
                  onClick={handleDivisionSave}
                  disabled={!canEditLeague}
                >
                  Save Division
                </button>
              </>
            ) : (
              <p className="muted">Select a division to edit.</p>
            )}
          </div>
        </div>
      </section>

      <section className="panel">
        <div className="panel-header">
          <h2>Teams</h2>
          <p>Manage team rosters for the selected league.</p>
        </div>
        <div className="grid three">
          <div>
            <ul className="list">
              {teams.map((team) => (
                <li
                  key={`${team.division}-${team.teamId}`}
                  className={`list-item ${selectedTeam?.teamId === team.teamId && selectedTeam?.division === team.division ? 'selected' : ''}`}
                  onClick={() => handleTeamSelect({ ...team })}
                >
                  <div>
                    <strong>{team.name}</strong>
                    <div className="muted">
                      {team.division} · {team.teamId}
                    </div>
                  </div>
                </li>
              ))}
            </ul>
          </div>
          <form className="form" onSubmit={handleTeamCreate}>
            <h3>Create Team</h3>
            <label className="field">
              <span>Division</span>
              <input
                value={teamForm.division}
                onChange={(event) =>
                  setTeamForm({ ...teamForm, division: event.target.value })
                }
                disabled={!canEditLeague}
              />
            </label>
            <label className="field">
              <span>Team ID</span>
              <input
                value={teamForm.teamId}
                onChange={(event) =>
                  setTeamForm({ ...teamForm, teamId: event.target.value })
                }
                disabled={!canEditLeague}
              />
            </label>
            <label className="field">
              <span>Name</span>
              <input
                value={teamForm.name}
                onChange={(event) =>
                  setTeamForm({ ...teamForm, name: event.target.value })
                }
                disabled={!canEditLeague}
              />
            </label>
            <label className="field">
              <span>Contact Name</span>
              <input
                value={teamForm.primaryContact.name}
                onChange={(event) =>
                  setTeamForm({
                    ...teamForm,
                    primaryContact: {
                      ...teamForm.primaryContact,
                      name: event.target.value,
                    },
                  })
                }
                disabled={!canEditLeague}
              />
            </label>
            <label className="field">
              <span>Contact Email</span>
              <input
                value={teamForm.primaryContact.email}
                onChange={(event) =>
                  setTeamForm({
                    ...teamForm,
                    primaryContact: {
                      ...teamForm.primaryContact,
                      email: event.target.value,
                    },
                  })
                }
                disabled={!canEditLeague}
              />
            </label>
            <label className="field">
              <span>Contact Phone</span>
              <input
                value={teamForm.primaryContact.phone}
                onChange={(event) =>
                  setTeamForm({
                    ...teamForm,
                    primaryContact: {
                      ...teamForm.primaryContact,
                      phone: event.target.value,
                    },
                  })
                }
                disabled={!canEditLeague}
              />
            </label>
            <button type="submit" disabled={!canEditLeague}>
              Create Team
            </button>
          </form>
          <div className="form">
            <h3>Team Details</h3>
            {selectedTeam ? (
              <>
                <label className="field">
                  <span>Division</span>
                  <input value={selectedTeam.division} disabled />
                </label>
                <label className="field">
                  <span>Team ID</span>
                  <input value={selectedTeam.teamId} disabled />
                </label>
                <label className="field">
                  <span>Name</span>
                  <input
                    value={selectedTeam.name}
                    onChange={(event) =>
                      setSelectedTeam({
                        ...selectedTeam,
                        name: event.target.value,
                      })
                    }
                    disabled={!canEditLeague}
                  />
                </label>
                <label className="field">
                  <span>Contact Name</span>
                  <input
                    value={selectedTeam.primaryContact?.name || ''}
                    onChange={(event) =>
                      setSelectedTeam({
                        ...selectedTeam,
                        primaryContact: {
                          ...selectedTeam.primaryContact,
                          name: event.target.value,
                        },
                      })
                    }
                    disabled={!canEditLeague}
                  />
                </label>
                <label className="field">
                  <span>Contact Email</span>
                  <input
                    value={selectedTeam.primaryContact?.email || ''}
                    onChange={(event) =>
                      setSelectedTeam({
                        ...selectedTeam,
                        primaryContact: {
                          ...selectedTeam.primaryContact,
                          email: event.target.value,
                        },
                      })
                    }
                    disabled={!canEditLeague}
                  />
                </label>
                <label className="field">
                  <span>Contact Phone</span>
                  <input
                    value={selectedTeam.primaryContact?.phone || ''}
                    onChange={(event) =>
                      setSelectedTeam({
                        ...selectedTeam,
                        primaryContact: {
                          ...selectedTeam.primaryContact,
                          phone: event.target.value,
                        },
                      })
                    }
                    disabled={!canEditLeague}
                  />
                </label>
                <div className="action-row">
                  <button type="button" onClick={handleTeamSave} disabled={!canEditLeague}>
                    Save Team
                  </button>
                  <button
                    type="button"
                    className="danger"
                    onClick={handleTeamDelete}
                    disabled={!canEditLeague}
                  >
                    Delete Team
                  </button>
                </div>
              </>
            ) : (
              <p className="muted">Select a team to edit.</p>
            )}
          </div>
        </div>
      </section>
    </div>
  )
}

export default App

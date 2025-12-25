import './App.css'

const componentAudit = [
  {
    name: 'Buttons',
    description: 'Primary, secondary, and ghost actions with consistent sizing and icon support.',
  },
  {
    name: 'Form fields',
    description: 'Stacked labels, inline helper text, validation placeholders, and keyboard-first focus states.',
  },
  {
    name: 'Cards & panels',
    description: 'Structured containers for workflows, summaries, and bulk actions.',
  },
  {
    name: 'Badges',
    description: 'Status indicators for approval, draft, and published states.',
  },
  {
    name: 'Tables',
    description: 'Sortable lists with bulk select checkboxes and inline actions.',
  },
  {
    name: 'Tooltips & inline guidance',
    description: 'ARIA-compliant tips for field intent, requirements, and bulk uploads.',
  },
]

const workflowSteps = {
  createEvent: [
    'Define the event name, host organization, and sport category.',
    'Set date/time, location, capacity, and pricing details.',
    'Publish the event and notify registrants via email/SMS.',
  ],
  postOpportunity: [
    'Describe the opportunity and skill level requirements.',
    'Select target audiences (teams, leagues, age groups).',
    'Publish and monitor applicant submissions.',
  ],
  manageStructures: [
    'Create teams and assign staff/coach roles.',
    'Group teams into leagues and divisions.',
    'Schedule seasons and track standings.',
  ],
  bulkImport: [
    'Download the CSV template with required columns.',
    'Validate data in the preview step and resolve errors.',
    'Confirm import and review audit logs.',
  ],
}

function Tooltip({ id, label, text }) {
  return (
    <span className="tooltip">
      <button type="button" className="tooltip-trigger" aria-describedby={id} aria-label={label}>
        ?
      </button>
      <span role="tooltip" id={id} className="tooltip-content">
        {text}
      </span>
    </span>
  )
}

function App() {
  return (
    <div className="app">
      <header className="app-header">
        <div>
          <p className="eyebrow">FSA Admin Experience</p>
          <h1>Operations UX audit and workflow guidance</h1>
          <p className="subtitle">
            Standardized components, end-to-end workflows, and accessibility enhancements for
            high-impact admin actions.
          </p>
        </div>
        <div className="header-actions">
          <button className="button primary">Launch Admin Console</button>
          <button className="button ghost">View Release Notes</button>
        </div>
      </header>

      <section className="section">
        <div className="section-header">
          <h2>Component library audit</h2>
          <p>
            Standard components and UX patterns expected across <span className="code">fsa-www/</span>.
          </p>
        </div>
        <div className="card-grid">
          {componentAudit.map((item) => (
            <article key={item.name} className="card">
              <div className="card-header">
                <h3>{item.name}</h3>
                <span className="badge">Standard</span>
              </div>
              <p>{item.description}</p>
            </article>
          ))}
        </div>
      </section>

      <section className="section">
        <div className="section-header">
          <h2>End-to-end workflows</h2>
          <p>Documented journeys for high-impact actions.</p>
        </div>
        <div className="workflow-grid">
          <article className="workflow-card">
            <div className="workflow-header">
              <h3>Create Event</h3>
              <span className="badge success">Priority</span>
            </div>
            <ol>
              {workflowSteps.createEvent.map((step) => (
                <li key={step}>{step}</li>
              ))}
            </ol>
            <button className="button secondary">Open Event Builder</button>
          </article>
          <article className="workflow-card">
            <div className="workflow-header">
              <h3>Post Opportunity</h3>
              <span className="badge">Pipeline</span>
            </div>
            <ol>
              {workflowSteps.postOpportunity.map((step) => (
                <li key={step}>{step}</li>
              ))}
            </ol>
            <button className="button secondary">Draft Opportunity</button>
          </article>
          <article className="workflow-card">
            <div className="workflow-header">
              <h3>Manage Teams/Leagues/Divisions</h3>
              <span className="badge warn">Admin</span>
            </div>
            <ol>
              {workflowSteps.manageStructures.map((step) => (
                <li key={step}>{step}</li>
              ))}
            </ol>
            <button className="button secondary">Manage Structures</button>
          </article>
          <article className="workflow-card">
            <div className="workflow-header">
              <h3>Bulk Import</h3>
              <span className="badge">Automation</span>
            </div>
            <ol>
              {workflowSteps.bulkImport.map((step) => (
                <li key={step}>{step}</li>
              ))}
            </ol>
            <button className="button secondary">Start Import</button>
          </article>
        </div>
      </section>

      <section className="section">
        <div className="section-header">
          <h2>Guided action forms</h2>
          <p>Inline guidance and ARIA-compliant tooltips for every key action.</p>
        </div>
        <div className="forms-grid">
          <form className="form-card" aria-label="Create event form">
            <div className="form-header">
              <h3>Create Event</h3>
              <p>Capture essential event details before publishing.</p>
            </div>
            <div className="form-field">
              <label htmlFor="event-name">Event name</label>
              <div className="field-input">
                <input
                  id="event-name"
                  name="event-name"
                  placeholder="Fall Invitational"
                  aria-describedby="event-name-help event-name-tip"
                />
                <Tooltip
                  id="event-name-tip"
                  label="Event name guidance"
                  text="Use a descriptive, season-specific name to aid search and analytics."
                />
              </div>
              <p className="field-help" id="event-name-help">
                Appears on public listings and confirmation emails.
              </p>
            </div>
            <div className="form-field">
              <label htmlFor="event-date">Date & time</label>
              <div className="field-input">
                <input
                  id="event-date"
                  name="event-date"
                  placeholder="Oct 14, 9:00 AM - 3:00 PM"
                  aria-describedby="event-date-help event-date-tip"
                />
                <Tooltip
                  id="event-date-tip"
                  label="Date and time guidance"
                  text="Confirm the venue availability and ensure time zone accuracy."
                />
              </div>
              <p className="field-help" id="event-date-help">
                Include time zone to avoid participant confusion.
              </p>
            </div>
            <div className="form-actions">
              <button className="button primary" type="button">
                Save draft
              </button>
              <button className="button ghost" type="button">
                Preview listing
              </button>
            </div>
          </form>

          <form className="form-card" aria-label="Post opportunity form">
            <div className="form-header">
              <h3>Post Opportunity</h3>
              <p>Clarify the role and visibility for potential applicants.</p>
            </div>
            <div className="form-field">
              <label htmlFor="opportunity-role">Role title</label>
              <div className="field-input">
                <input
                  id="opportunity-role"
                  name="opportunity-role"
                  placeholder="Assistant Coach"
                  aria-describedby="opportunity-role-help opportunity-role-tip"
                />
                <Tooltip
                  id="opportunity-role-tip"
                  label="Role title guidance"
                  text="Use a common title so candidates can filter and search quickly."
                />
              </div>
              <p className="field-help" id="opportunity-role-help">
                Keep the title under 60 characters for mobile display.
              </p>
            </div>
            <div className="form-field">
              <label htmlFor="opportunity-audience">Target audience</label>
              <div className="field-input">
                <select
                  id="opportunity-audience"
                  name="opportunity-audience"
                  aria-describedby="opportunity-audience-help opportunity-audience-tip"
                >
                  <option>Youth league coaches</option>
                  <option>High school athletic directors</option>
                  <option>Volunteer coordinators</option>
                </select>
                <Tooltip
                  id="opportunity-audience-tip"
                  label="Target audience guidance"
                  text="Pick the group that aligns with your required certifications."
                />
              </div>
              <p className="field-help" id="opportunity-audience-help">
                Audience selection controls email and in-app notification segments.
              </p>
            </div>
            <div className="form-actions">
              <button className="button primary" type="button">
                Publish opportunity
              </button>
              <button className="button ghost" type="button">
                Save as draft
              </button>
            </div>
          </form>

          <form className="form-card" aria-label="Manage teams form">
            <div className="form-header">
              <h3>Manage Teams/Leagues/Divisions</h3>
              <p>Maintain accurate org structure for scheduling and reporting.</p>
            </div>
            <div className="form-field">
              <label htmlFor="team-league">League name</label>
              <div className="field-input">
                <input
                  id="team-league"
                  name="team-league"
                  placeholder="North Metro League"
                  aria-describedby="team-league-help team-league-tip"
                />
                <Tooltip
                  id="team-league-tip"
                  label="League name guidance"
                  text="Use geographic identifiers to distinguish multiple leagues."
                />
              </div>
              <p className="field-help" id="team-league-help">
                Leagues can include multiple divisions and age groups.
              </p>
            </div>
            <div className="form-field">
              <label htmlFor="division-count">Divisions</label>
              <div className="field-input">
                <input
                  id="division-count"
                  name="division-count"
                  type="number"
                  placeholder="3"
                  aria-describedby="division-count-help division-count-tip"
                />
                <Tooltip
                  id="division-count-tip"
                  label="Division count guidance"
                  text="Start with the default divisions and adjust after team assignments."
                />
              </div>
              <p className="field-help" id="division-count-help">
                Divisions can be renamed later in the season settings.
              </p>
            </div>
            <div className="form-actions">
              <button className="button primary" type="button">
                Update structure
              </button>
              <button className="button ghost" type="button">
                View roster
              </button>
            </div>
          </form>

          <form className="form-card" aria-label="Bulk import form">
            <div className="form-header">
              <h3>Bulk Import</h3>
              <p>Upload teams, players, or schedules from CSV.</p>
            </div>
            <div className="form-field">
              <label htmlFor="import-type">Import type</label>
              <div className="field-input">
                <select
                  id="import-type"
                  name="import-type"
                  aria-describedby="import-type-help import-type-tip"
                >
                  <option>Teams</option>
                  <option>Players</option>
                  <option>Schedules</option>
                </select>
                <Tooltip
                  id="import-type-tip"
                  label="Import type guidance"
                  text="Choose the data set that matches your CSV template."
                />
              </div>
              <p className="field-help" id="import-type-help">
                Use UTF-8 encoded CSVs and include all required columns.
              </p>
            </div>
            <div className="form-field">
              <label htmlFor="import-file">Upload file</label>
              <div className="field-input">
                <input
                  id="import-file"
                  name="import-file"
                  type="file"
                  aria-describedby="import-file-help import-file-tip"
                />
                <Tooltip
                  id="import-file-tip"
                  label="File upload guidance"
                  text="Max 10MB. Files are scanned for formatting errors before import."
                />
              </div>
              <p className="field-help" id="import-file-help">
                Preview results before committing to the import.
              </p>
            </div>
            <div className="form-actions">
              <button className="button primary" type="button">
                Validate file
              </button>
              <button className="button ghost" type="button">
                Download template
              </button>
            </div>
          </form>
        </div>
      </section>
    </div>
  )
}

export default App

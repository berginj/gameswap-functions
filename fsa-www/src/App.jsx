import { useMemo, useState } from 'react'
import './App.css'

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
  const [apiBase, setApiBase] = useState('http://localhost:7071')
  const [leagueId, setLeagueId] = useState('')
  const [selectedKey, setSelectedKey] = useState('fields')
  const [csvPreview, setCsvPreview] = useState([])
  const [gridPreview, setGridPreview] = useState([])
  const [gridText, setGridText] = useState('')
  const [useHeaderRow, setUseHeaderRow] = useState(true)
  const [status, setStatus] = useState(null)
  const [errors, setErrors] = useState([])

  const selectedType = useMemo(
    () => IMPORT_TYPES.find((type) => type.key === selectedKey),
    [selectedKey]
  )

  const handleCsvFile = async (event) => {
    const file = event.target.files?.[0]
    if (!file) return
    const text = await file.text()
    const rows = parseCsv(text)
    setCsvPreview(rows.slice(0, 6))
    setStatus(null)
    setErrors([])

    const formData = new FormData()
    formData.append('file', file)

    const response = await fetch(`${apiBase}/api/import/${selectedKey}`, {
      method: 'POST',
      headers: {
        'x-league-id': leagueId
      },
      body: formData
    })

    const payload = await response.json().catch(() => null)
    setStatus(payload)
    setErrors(payload?.data?.errors ?? payload?.error?.details?.errors ?? [])
  }

  const handlePastePreview = () => {
    const rows = parseGridText(gridText)
    const header = useHeaderRow ? rows[0] ?? [] : selectedType.csvHeaders
    const dataRows = useHeaderRow ? rows.slice(1) : rows
    const previewRows = [header, ...dataRows].slice(0, 6)
    setGridPreview(previewRows)
  }

  const handleGridImport = async () => {
    const rows = parseGridText(gridText)
    const headers = useHeaderRow ? rows[0] ?? [] : selectedType.csvHeaders
    const dataRows = useHeaderRow ? rows.slice(1) : rows

    const response = await fetch(`${apiBase}/api/import/${selectedKey}/grid`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'x-league-id': leagueId
      },
      body: JSON.stringify({ headers, rows: dataRows })
    })

    const payload = await response.json().catch(() => null)
    setStatus(payload)
    setErrors(payload?.data?.errors ?? payload?.error?.details?.errors ?? [])
  }

  return (
    <div className="app">
      <header className="app__header">
        <h1>Bulk Import</h1>
        <p>Upload CSVs or paste grid data for fields, teams, and events.</p>
      </header>

      <section className="card form-section">
        <div className="form-row">
          <label>
            API Base URL
            <input
              value={apiBase}
              onChange={(event) => setApiBase(event.target.value)}
              placeholder="http://localhost:7071"
            />
          </label>
          <label>
            League ID
            <input
              value={leagueId}
              onChange={(event) => setLeagueId(event.target.value)}
              placeholder="league_123"
            />
          </label>
        </div>
      </section>

      <section className="card form-section">
        <div className="type-selector">
          {IMPORT_TYPES.map((type) => (
            <button
              key={type.key}
              className={selectedKey === type.key ? 'active' : ''}
              onClick={() => setSelectedKey(type.key)}
              type="button"
            >
              {type.label}
            </button>
          ))}
        </div>

        <div className="template-row">
          <span>Template:</span>
          <button
            type="button"
            onClick={() => downloadCsvTemplate(selectedType.csvHeaders, `${selectedType.key}-template.csv`)}
          >
            Download CSV template
          </button>
        </div>
      </section>

      <section className="grid">
        <div className="card">
          <h2>CSV Upload</h2>
          <p>Upload a CSV file to import {selectedType.label.toLowerCase()}.</p>
          <input type="file" accept=".csv" onChange={handleCsvFile} />
          {csvPreview.length > 0 && (
            <div className="preview">
              <h3>CSV Preview</h3>
              <PreviewTable rows={csvPreview} />
            </div>
          )}
        </div>

        <div className="card">
          <h2>Paste to Grid</h2>
          <p>Paste rows copied from Excel or Google Sheets (tab-separated).</p>
          <textarea
            value={gridText}
            onChange={(event) => setGridText(event.target.value)}
            placeholder="Paste rows here..."
            rows={8}
          />
          <div className="toggle-row">
            <label>
              <input
                type="checkbox"
                checked={useHeaderRow}
                onChange={(event) => setUseHeaderRow(event.target.checked)}
              />
              First row contains headers
            </label>
          </div>
          <div className="button-row">
            <button type="button" onClick={handlePastePreview}>
              Preview
            </button>
            <button type="button" className="primary" onClick={handleGridImport}>
              Import grid data
            </button>
          </div>
          {gridPreview.length > 0 && (
            <div className="preview">
              <h3>Grid Preview</h3>
              <PreviewTable rows={gridPreview} />
            </div>
          )}
        </div>
      </section>

      <section className="card">
        <h2>Import Status</h2>
        <p>Results from the most recent import request.</p>
        <pre className="status-block">{status ? JSON.stringify(status, null, 2) : 'No import executed yet.'}</pre>
      </section>

      <section className="card">
        <h2>Errors</h2>
        {errors.length === 0 ? (
          <p>No errors to show.</p>
        ) : (
          <ul className="error-list">
            {errors.map((error, index) => (
              <li key={`${error.row}-${error.column}-${index}`}>
                <strong>Row {error.row}</strong> — {error.column}: {error.reason}
                {error.value ? <em> ({error.value})</em> : null}
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  )
}

const PreviewTable = ({ rows }) => {
  if (!rows || rows.length === 0) return null
  const header = rows[0]
  const body = rows.slice(1)

  return (
    <div className="table-wrapper">
      <table>
        <thead>
          <tr>
            {header.map((cell, index) => (
              <th key={`head-${index}`}>{cell}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {body.map((row, rowIndex) => (
            <tr key={`row-${rowIndex}`}>
              {row.map((cell, index) => (
                <td key={`cell-${rowIndex}-${index}`}>{cell}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

export default App

# GameSwap API Contract

This document is the single source of truth for UI↔API integration. It must be kept identical in both repos:
- UI repo: /docs/contract.md
- API repo: /docs/contract.md

## Cross-cutting rules

### Auth
- All calls assume the user is authenticated via Azure Static Web Apps EasyAuth.
- The API may return 401 when the user is not signed in.

### League scoping (non-negotiable)
- Every league-scoped endpoint requires header: x-league-id: <leagueId>
- Backend validates header presence and authorization (membership or global admin where specified).
- UI persists the selected league id and attaches it on every league-scoped request.

### Roles (locked)
League role strings:
- LeagueAdmin: can manage league setup (fields, divisions/templates, teams), update league contact, and perform all scheduler actions. Second only to global admin.
- Coach: can be approved before a team is assigned. A LeagueAdmin can assign (or change) the coach's team later. Coaches can offer slots, request swaps, and approve/deny slot requests. Some actions (like requesting a swap) may require a team assignment.
- Viewer: read-only. Can view available games/slots and upcoming schedule views. Cannot offer, request, approve, or manage setup.

Global admin:
- isGlobalAdmin is returned by /me. Global admins can create leagues and can perform any league-scoped admin action.

### Standard response envelope (non-negotiable)
All endpoints return JSON with one of:
- Success: { "data": ... }
- Failure: { "error": { "code": string, "message": string, "details"?: any } }

### Error codes (recommended)
- BAD_REQUEST (400)
- UNAUTHENTICATED (401)
- FORBIDDEN (403)
- NOT_FOUND (404)
- CONFLICT (409)
- INTERNAL (500)


### Time conventions (locked)
All schedule times are interpreted as **US/Eastern (America/New_York)**. The API stores and returns:
- `gameDate` / `eventDate` as `YYYY-MM-DD`
- `startTime` / `endTime` as `HH:MM` (24-hour)
The API does **not** convert between time zones.

---

## 1) Onboarding

### GET /me
Returns identity and memberships.

Response
```json
{
  "data": {
    "userId": "<string>",
    "email": "<string>",
    "isGlobalAdmin": false,
    "memberships": [
      { "leagueId": "ARL", "role": "LeagueAdmin" },
      { "leagueId": "ARL", "role": "Coach" },
      { "leagueId": "ARL", "role": "Coach", "team": { "division": "10U", "teamId": "TIGERS" } },
      { "leagueId": "XYZ", "role": "Viewer" }
    ]
  }
}
```

---

## 2) Function call reference (authoritative)

This section is the **artifact for automated reviewers** (including Codex) to understand how every HTTP-triggered
function is called. Keep it current any time a route, method, or authorization requirement changes. The table below
is intentionally minimal: it defines how to call each function and where to find the implementation.

**For Codex reviewers:** treat this section as the canonical API call index. Read the table for routes/methods and
the notes for required headers or roles.

### Maintenance rules
- Every HttpTrigger must appear in the table below with its HTTP method(s) and route.
- If an endpoint becomes league-scoped, call out the `x-league-id` requirement in its notes.
- When refactoring or adding functions, update this table and the matching UI repo copy.

### Endpoint index

| Method | Route | File | Notes |
| --- | --- | --- | --- |
| GET | /ping | `Functions/Ping.cs` | Health check. |
| GET | /me | `Functions/GetMe.cs` | Authenticated identity and memberships. |
| GET | /leagues | `Functions/LeaguesFunctions.cs` | List leagues for current user. |
| GET | /league | `Functions/LeaguesFunctions.cs` | Get current league details (requires `x-league-id`). |
| PATCH | /league | `Functions/LeaguesFunctions.cs` | Update current league (requires `x-league-id`, LeagueAdmin). |
| GET | /admin/leagues | `Functions/LeaguesFunctions.cs` | Global admin list of leagues. |
| POST | /admin/leagues | `Functions/LeaguesFunctions.cs` | Global admin create league. |
| GET | /admin/globaladmins | `Functions/GlobalAdminsFunctions.cs` | Global admin list. |
| POST | /admin/globaladmins | `Functions/GlobalAdminsFunctions.cs` | Add global admin. |
| DELETE | /admin/globaladmins/{userId} | `Functions/GlobalAdminsFunctions.cs` | Remove global admin. |
| POST | /accessrequests | `Functions/AccessRequestsFunctions.cs` | Create access request. |
| GET | /accessrequests/mine | `Functions/AccessRequestsFunctions.cs` | Current user's access requests. |
| GET | /accessrequests | `Functions/AccessRequestsFunctions.cs` | League access requests (requires `x-league-id`, LeagueAdmin). |
| PATCH | /accessrequests/{userId}/approve | `Functions/AccessRequestsFunctions.cs` | Approve access request (requires `x-league-id`, LeagueAdmin). |
| PATCH | /accessrequests/{userId}/deny | `Functions/AccessRequestsFunctions.cs` | Deny access request (requires `x-league-id`, LeagueAdmin). |
| POST | /admin/invites | `Functions/LeagueInvitesFunctions.cs` | Create invite (requires `x-league-id`, LeagueAdmin). |
| POST | /invites/accept | `Functions/LeagueInvitesFunctions.cs` | Accept invite. |
| GET | /memberships | `Functions/MembershipsFunctions.cs` | List memberships (requires `x-league-id`, LeagueAdmin). |
| PATCH | /memberships/{userId} | `Functions/MembershipsFunctions.cs` | Update membership (requires `x-league-id`, LeagueAdmin). |
| GET | /divisions | `Functions/DivisionsFunctions.cs` | List divisions (requires `x-league-id`). |
| POST | /divisions | `Functions/DivisionsFunctions.cs` | Create division (requires `x-league-id`, LeagueAdmin). |
| PATCH | /divisions/{code} | `Functions/DivisionsFunctions.cs` | Update division (requires `x-league-id`, LeagueAdmin). |
| GET | /divisions/templates | `Functions/DivisionsFunctions.cs` | List division templates (requires `x-league-id`). |
| PATCH | /divisions/templates | `Functions/DivisionsFunctions.cs` | Update division templates (requires `x-league-id`, LeagueAdmin). |
| GET | /teams | `Functions/TeamsFunctions.cs` | List teams (requires `x-league-id`). |
| POST | /teams | `Functions/TeamsFunctions.cs` | Create team (requires `x-league-id`, LeagueAdmin). |
| PATCH | /teams/{division}/{teamId} | `Functions/TeamsFunctions.cs` | Update team (requires `x-league-id`, LeagueAdmin). |
| DELETE | /teams/{division}/{teamId} | `Functions/TeamsFunctions.cs` | Delete team (requires `x-league-id`, LeagueAdmin). |
| GET | /fields | `Functions/FieldsFunctions.cs` | List fields (requires `x-league-id`). |
| POST | /import/fields | `Functions/ImportFields.cs` | CSV field import (requires `x-league-id`, LeagueAdmin). |
| POST | /import/slots | `Functions/ImportSlots.cs` | CSV slot import (requires `x-league-id`, LeagueAdmin). |
| GET | /slots | `Functions/GetSlots.cs` | List slots (requires `x-league-id`). |
| POST | /slots | `Functions/CreateSlot.cs` | Create slot (requires `x-league-id`, LeagueAdmin). |
| PATCH | /slots/{division}/{slotId}/cancel | `Functions/CancelSlot.cs` | Cancel slot (requires `x-league-id`, LeagueAdmin). |
| GET | /slots/{division}/{slotId}/requests | `Functions/GetSlotRequests.cs` | List requests for slot (requires `x-league-id`). |
| POST | /slots/{division}/{slotId}/requests | `Functions/CreateSlotRequest.cs` | Request slot (requires `x-league-id`, Coach). |
| PATCH | /slots/{division}/{slotId}/requests/{requestId}/approve | `Functions/ApproveSlotRequest.cs` | Approve slot request (requires `x-league-id`, Coach). |
| GET | /events | `Functions/GetEvents.cs` | List events (requires `x-league-id`). |
| POST | /events | `Functions/CreateEvent.cs` | Create event (requires `x-league-id`, LeagueAdmin). |
| PATCH | /events/{eventId} | `Functions/PatchEvent.cs` | Update event (requires `x-league-id`, LeagueAdmin). |
| DELETE | /events/{eventId} | `Functions/DeleteEvent.cs` | Delete event (requires `x-league-id`, LeagueAdmin). |

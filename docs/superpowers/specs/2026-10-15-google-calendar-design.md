# Google Calendar – Design

Approved by the user on 2026-10-15.

## Goal
Show the user's Google Calendar events for the day in the agenda sidebar, and let individual time entries be pushed to a dedicated "Agender" Google calendar.

## Decisions
- Direction: read and write. Read = events of a calendar the user shared with their service account (live, never stored). Write = only entries flagged `SyncToGoogle`, into the same calendar or an optional separate one.
- The app is the source of truth: edits made in Google to events the app created are never read back; the next push overwrites them. Events written by the app carry a private extended property and are skipped when reading.
- Display: a "Calendário" tab in the agenda sidebar listing the events of the visible day; each timed event has a "Criar registo" action that opens the entry editor prefilled.
- Credential model (revised on 2026-10-15, replaces the original OAuth flow): each user pastes the JSON key of a Google service account in Settings. There is no server-side Google configuration, no OAuth callback and no browser round trip.
- Scope: `calendar.events`.

## Connection
- `PUT /api/google` with `{ serviceAccountJson, calendarId, writeCalendarId? }` validates the key (must be `type: service_account`; `token_uri` is ignored so the server only ever calls Google's token endpoint) and probes both calendars; failure to access a calendar is a 400.
- The key is stored per user in `GoogleConnection`, protected with Data Protection, with the service account email, the calendar ids and the time zone of the write calendar. The status endpoint exposes only the email and calendar ids.
- Changing the write calendar unlinks the already created events (flagged entries are re-sent by "Sincronizar agora"). Disconnect removes the connection and clears the sync flags of all entries.

## Writing entries
- `TimeEntry` gains `SyncToGoogle`, `GoogleSyncFailed` (both exposed in the DTO) and `GoogleEventId` (server only).
- A decorator over `ITimeEntryService` runs the sync after create/update (upsert when flagged, delete the linked event when unflagged) and before delete.
- Google failures never fail the save: the entry is stored with `GoogleSyncFailed = true`. "Sincronizar agora" in Settings retries every flagged entry that failed or is unlinked.
- Known limit: if Google is unreachable when an entry is deleted, its event stays orphaned.
- Event times are written in the time zone of the write calendar; read times are those of the read calendar.

## Layers
- `IGoogleCalendarApi` (Infrastructure): thin wrapper over the official Google libraries, faked in tests.
- `GoogleCalendarService` (Infrastructure): connection, events, sync logic; implements `IGoogleCalendarService` (Shared contract).
- `ApiGoogleCalendarService` (MAUI): the same contract through the server API.
- `GoogleCalendarState` (Shared): scoped UI state (status, cached day events).

## Out of scope
Other calendars, automatic import, reading changes back, notifications, delete queue.

## Testing
Service tests with a fake `IGoogleCalendarApi` (connect, validation failure, events, upsert/delete/failure/retry, disconnect, user isolation), key parser tests, decorator tests and API tests for status/connect/events. Not testable here: the real Google round trip (needs a real service account).

# Agender – Design

Approved by the user on 2026-10-12.

## Goal
A premium, fast and responsive agenda where time entries are logged to the minute describing what was done, with a task repository whose items can be dragged onto the agenda, and a note per day edited in place.

## Solution layout (template `maui-blazor-web`, .NET 10)
| Project | Responsibility |
|---|---|
| `Agender.Shared` | Razor class library: domain, EF Core/SQLite data layer, services, all UI |
| `Agender.Web` | Blazor Web App (interactive Server) host |
| `Agender` | MAUI Blazor Hybrid host (Windows target is the one verified) |
| `Agender.Tests` | xUnit + FluentAssertions; domain logic and services (SQLite in-memory) |

## Domain
- `TimeEntry`: `Id`, `Date` (DateOnly), `StartMinute`/`EndMinute` (minutes since midnight, 0..1440, end > start), `Description`, `Color`, optional `TaskItemId`.
- `TaskItem`: `Id`, `Title`, `Color`, `IsCompleted`, `CreatedAt`.
- `DayNote`: `Date` (key), `Text`.
- `TimeSlot` value object: validation, overlap check, snapping.
- `EntryLayout`: assigns side-by-side lanes to overlapping entries of one day.

## Services (interfaces in Shared, DI, `CancellationToken` on all async methods)
- `ITimeEntryService`: range query, create, update, delete.
- `ITaskService`: list, create, update, set completed, delete.
- `IDayNoteService`: range query, save (empty text deletes).
Services use `IDbContextFactory<AgenderDbContext>`. Invalid input throws `ArgumentException` (validated by `TimeSlot`).

## Persistence
SQLite + EF Core migrations (in Shared). Database file path is supplied by the host: Web → `<content root>/App_Data/agender.db`; MAUI → `FileSystem.AppDataDirectory`. Migrations are applied at startup.

## UI
- Day / Week views of the same `Timeline` component; 24h grid, 72 px per hour, snap selectable (1 / 5 / 15 min), "now" line, auto-scroll to the working hours.
- Pointer interactions are handled in a collocated JS module (no server round-trip while dragging): create by dragging on empty space, move (also across days), resize from top/bottom edges, click to edit. Results are sent to .NET via `DotNetObjectReference`.
- Task repository sidebar (collapsible; drawer on mobile). Cards use HTML5 drag; dropping on the grid creates a 30-minute entry with the task title.
- Day note shown in the day header, edited in place with debounced autosave.
- Entry editor modal: description, start/end (minute precision), color, delete.
- Light/dark theme (system default + toggle), design tokens in CSS variables, isolated CSS per component, no UI framework.

## Out of scope
Accounts, cloud sync, reports/export, recurrence.

## Verification
`dotnet test`; `dotnet build` of Web and the MAUI Windows target; run Web and check it serves.
